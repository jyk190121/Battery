using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

/// <summary>
/// 게임 내 모든 퀘스트의 생성, 수락, 진행도 체크, 클리어 판정 및 기믹 스폰을 총괄하는 중앙 매니저.
/// </summary>
public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance;

    [Header("Quest Database")]
    public List<QuestDataSO> questDatabase;

    [Header("Sync Lists (Selected & Completed)")]
    public NetworkList<int> activeQuests;
    public NetworkList<int> serverCompletedQuests;
    public NetworkList<int> itemsInTruck;

    [Header("Daily Offered Pools (Difficulty Based)")]
    public NetworkList<int> easyOffered;
    public NetworkList<int> normalOffered;
    public NetworkList<int> hardOffered;

    public NetworkVariable<QuestDifficulty> selectedDifficulty = new NetworkVariable<QuestDifficulty>(
        QuestDifficulty.Easy,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private List<int> myActuallyDoneQuests = new List<int>();
    private Dictionary<int, List<QuestReturnPoint>> returnPointRegistry = new Dictionary<int, List<QuestReturnPoint>>();


    // ==========================================
    // 1. 생명주기 및 초기화
    // ==========================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        activeQuests = new NetworkList<int>();
        serverCompletedQuests = new NetworkList<int>();
        itemsInTruck = new NetworkList<int>();
        easyOffered = new NetworkList<int>();
        normalOffered = new NetworkList<int>();
        hardOffered = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            RefreshDailyQuestPools();
        }
    }


    // ==========================================
    // 2. 퀘스트 생성 및 유지보수
    // ==========================================

    [ServerRpc]
    public void RefreshDailyQuestPoolsServerRpc()
    {
        RefreshDailyQuestPools();
    }

    private void RefreshDailyQuestPools()
    {
        if (!IsServer) { return; }

        GenerateDifficultyPool(easyOffered, QuestDifficulty.Easy, 4);
        GenerateDifficultyPool(normalOffered, QuestDifficulty.Normal, 4);
        GenerateDifficultyPool(hardOffered, QuestDifficulty.Hard, 4);

        Debug.Log("<color=yellow>[Quest] 난이도별 4개 추출 풀 생성 완료.</color>");
    }

    private void GenerateDifficultyPool(NetworkList<int> targetList, QuestDifficulty targetDifficulty, int poolCount)
    {
        targetList.Clear();

        IEnumerable<QuestDataSO> generatedPool = questDatabase
            .Where(quest => quest.difficulty == targetDifficulty)
            .OrderBy(randomOrder => Random.value)
            .Take(poolCount);

        foreach (QuestDataSO quest in generatedPool)
        {
            targetList.Add(quest.questID);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AcceptDifficultyContractServerRpc(QuestDifficulty difficulty)
    {
        activeQuests.Clear();

        NetworkList<int> targetPool = difficulty switch
        {
            QuestDifficulty.Easy => easyOffered,
            QuestDifficulty.Normal => normalOffered,
            QuestDifficulty.Hard => hardOffered,
            _ => null
        };

        selectedDifficulty.Value = difficulty;

        if (targetPool != null)
        {
            string questListString = "";
            foreach (int questId in targetPool)
            {
                activeQuests.Add(questId);
                QuestDataSO questData = GetQuestData(questId);
                questListString += $" - [ID:{questId}] {(questData != null ? questData.questName : "Unknown")}\n";
            }
            Debug.Log($"<color=yellow><b>[QUEST START]</b></color> {difficulty} 난이도 계약 수락! (총 {activeQuests.Count}개)\n<color=white>{questListString}</color>");
        }
    }

    public void ResetDailyQuests()
    {
        if (!IsServer) { return; }

        activeQuests.Clear();
        serverCompletedQuests.Clear();
        itemsInTruck.Clear();

        RefreshDailyQuestPools();
        ResetLocalQuestsClientRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetLocalQuestsClientRpc()
    {
        myActuallyDoneQuests.Clear();
    }

    public QuestDataSO GetQuestData(int questId)
    {
        return questDatabase.Find(quest => quest.questID == questId);
    }

    public (int money, int score) GetCalculatedQuestResults()
    {
        if (!IsServer) { return (0, 0); }

        int totalMoney = 0;
        int totalScore = 0;

        foreach (int questId in serverCompletedQuests)
        {
            QuestDataSO questData = GetQuestData(questId);
            if (questData == null) { continue; }

            float totalMultiplier = 1.0f + questData.bonusMultiplier + (questData.isHazardQuest ? 0.2f : 0f);
            totalMoney += Mathf.RoundToInt(questData.baseReward * totalMultiplier);
            totalScore += questData.performancePoint;
        }

        return (totalMoney, totalScore);
    }


    // ==========================================
    // 3. 퀘스트 클리어 판정 로직
    // ==========================================

    public bool IsQuestCleared(int questID)
    {
        if (!activeQuests.Contains(questID)) { return false; }

        QuestDataSO questData = GetQuestData(questID);
        if (questData == null) { return false; }

        string questName = questData.questName;

        if (serverCompletedQuests.Contains(questID) || myActuallyDoneQuests.Contains(questID))
        {
            Debug.Log($"<color=lime>[Quest UI]</color> 퀘스트 완료: <b>[ID:{questID}] {questName}</b> (사유: 서버 기록 또는 확정 클리어)");
            return true;
        }

        if (questData.type == QuestType.Collect)
        {
            if (itemsInTruck.Contains(questData.targetItemID))
            {
                Debug.Log($"<color=lime>[Quest UI]</color> 퀘스트 완료: <b>[ID:{questID}] {questName}</b> (사유: 트럭 내 목표물({questData.targetItemID}) 감지됨)");
                return true;
            }
        }

        if (questData.type == QuestType.Photo || questData.type == QuestType.Record)
        {
            if (QuestCameraBridge.Instance != null && QuestCameraBridge.Instance.IsPhotoInLocalAlbum(questID))
            {
                Debug.Log($"<color=lime>[Quest UI]</color> 퀘스트 완료: <b>[ID:{questID}] {questName}</b> (사유: 개인 스마트폰 앨범에 사진 존재)");
                return true;
            }
        }

        return false;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void NotifyLocalClientToggleClientRpc(int questID, bool isCleared, RpcParams rpcParams)
    {
        if (isCleared)
        {
            if (!myActuallyDoneQuests.Contains(questID))
            {
                myActuallyDoneQuests.Add(questID);
            }
        }
        else
        {
            if (myActuallyDoneQuests.Contains(questID))
            {
                myActuallyDoneQuests.Remove(questID);
            }
        }

        Debug.Log($"<color=cyan><b>[MY QUEST]</b></color> 개인 폰 업데이트: ID {questID} -> {isCleared}");
    }

    public void NotifyFinalClear(int targetId, ulong solverId)
    {
        if (!IsServer) { return; }

        if (activeQuests.Contains(targetId))
        {
            MarkQuestAsComplete(targetId, solverId);
        }

        foreach (int activeQuestId in activeQuests)
        {
            QuestDataSO questData = GetQuestData(activeQuestId);
            if (questData != null && questData.type == QuestType.Collect && questData.targetItemID == targetId)
            {
                MarkQuestAsComplete(activeQuestId, solverId);
            }
        }
    }

    private void MarkQuestAsComplete(int questID, ulong solverId)
    {
        if (!serverCompletedQuests.Contains(questID))
        {
            serverCompletedQuests.Add(questID);

            QuestDataSO finalData = GetQuestData(questID);
            string questName = finalData != null ? finalData.questName : "Unknown";

            int totalActiveCount = activeQuests.Count;
            int clearedCount = serverCompletedQuests.Count;

            Debug.Log($"<color=lime><b>[SERVER MASTER]</b></color> 최종 클리어 확정: <color=white>[ID:{questID}] {questName}</color> (By: Client {solverId})");
            Debug.Log($"<color=lime><b>[SERVER MASTER]</b></color> 전체 진행도: {clearedCount}/{totalActiveCount}");
        }
    }

    public void NotifyCustomQuestMet(int questID, ulong solverId)
    {
        if (!IsServer || !activeQuests.Contains(questID)) { return; }

        NotifyFinalClear(questID, solverId);
        NotifyLocalClientToggleClientRpc(questID, true, RpcTarget.Single(solverId, RpcTargetUse.Temp));
    }


    // ==========================================
    // 4. 환원 포인트 및 특수 기믹 연동 (Gimmick Adapters)
    // ==========================================

    public void RegisterReturnPoint(int questID, QuestReturnPoint returnPoint)
    {
        if (!returnPointRegistry.ContainsKey(questID))
        {
            returnPointRegistry[questID] = new List<QuestReturnPoint>();
        }

        returnPointRegistry[questID].Add(returnPoint);
    }

    public void ActivateCurrentSceneReturnPoints()
    {
        foreach (List<QuestReturnPoint> pointList in returnPointRegistry.Values)
        {
            foreach (QuestReturnPoint point in pointList)
            {
                point.SetPointActivation(false);
            }
        }

        foreach (int questId in activeQuests)
        {
            if (returnPointRegistry.TryGetValue(questId, out List<QuestReturnPoint> registeredPoints))
            {
                foreach (QuestReturnPoint point in registeredPoints)
                {
                    point.SetPointActivation(true);
                }
            }
        }
    }

    /// <summary>
    /// 금고 내부 스폰 포인트를 전달받아 퀘스트 활성화 시 타겟 아이템을 스폰합니다.
    /// </summary>
    public bool TrySetupSafeGimmick(Transform safeInsidePoint)
    {
        if (!IsServer) { return false; }

        int[] safeQuestIds = { 1000, 2000, 3000 };
        int activeQuestId = 0;

        foreach (int questId in safeQuestIds)
        {
            if (activeQuests.Contains(questId))
            {
                activeQuestId = questId;
                break;
            }
        }

        if (activeQuestId == 0) { return false; }

        QuestDataSO questData = GetQuestData(activeQuestId);
        if (questData == null || questData.targetItemID == 0) { return false; }

        ItemBase targetPrefab = GameSessionManager.Instance.GetPrefab(questData.targetItemID);
        if (targetPrefab == null) { return false; }

        ItemBase spawnedItem = Instantiate(targetPrefab, safeInsidePoint.position, safeInsidePoint.rotation);
        spawnedItem.GetComponent<NetworkObject>().Spawn();

        return true;
    }

    public void AssignDailyGeneratorTargets()
    {
        if (!IsServer) { return; }

        List<GeneratorController> allGenerators = Object.FindObjectsByType<GeneratorController>(FindObjectsSortMode.None).ToList();
        List<DoorController> allDoors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None).ToList();

        int[] generatorQuestIds = { 1010, 2010, 3010 };
        int activeQuestId = 0;

        foreach (int questId in activeQuests)
        {
            if (generatorQuestIds.Contains(questId))
            {
                activeQuestId = questId;
                break;
            }
        }

        QuestDataSO targetQuestData = (activeQuestId != 0) ? GetQuestData(activeQuestId) : null;
        GeneratorController targetQuestGenerator = null;

        if (targetQuestData != null)
        {
            List<GeneratorController> validQuestGenerators = allGenerators.Where(generator =>
                generator.TryGetComponent(out QuestGeneratorAdapter adapter) &&
                generator.linkableDoors.Any(door => door != null && door.questItemSpawnPoint != null)
            ).ToList();

            if (validQuestGenerators.Count > 0)
            {
                targetQuestGenerator = validQuestGenerators[Random.Range(0, validQuestGenerators.Count)];
                List<DoorController> validDoors = targetQuestGenerator.linkableDoors.Where(door => door != null && door.questItemSpawnPoint != null).ToList();
                DoorController targetQuestDoor = validDoors[Random.Range(0, validDoors.Count)];

                allDoors.Remove(targetQuestDoor);
                targetQuestGenerator.GetComponent<QuestGeneratorAdapter>().SetupQuestTarget(targetQuestData, targetQuestDoor);
            }
        }

        foreach (GeneratorController generator in allGenerators)
        {
            if (generator == targetQuestGenerator) { continue; }

            List<DoorController> possibleDoors = allDoors.Where(door => generator.linkableDoors.Contains(door)).ToList();
            generator.linkableDoors.Clear();

            if (possibleDoors.Count > 0)
            {
                DoorController targetDoor = possibleDoors[Random.Range(0, possibleDoors.Count)];

                allDoors.Remove(targetDoor);
                generator.linkableDoors.Add(targetDoor);

                if (generator.TryGetComponent(out QuestGeneratorAdapter adapter))
                {
                    adapter.isQuestTarget.Value = false;
                }
                generator.enabled = true;
            }
        }
    }

    public void ActivateGeneratorGimmick()
    {
        if (!IsServer) { return; }

        AssignDailyGeneratorTargets();
        Debug.Log($"<color=lime>[Generator]</color> 일일 발전기 1:1 확정 매칭 완료.");
    }
}