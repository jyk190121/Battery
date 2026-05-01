using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance;

    [Header("Quest Database")]
    public List<QuestDataSO> questDatabase;

    [Header("Sync Lists (Selected & Completed)")]
    public NetworkList<int> activeQuests;           // 현재 수락한 퀘스트 4개
    public NetworkList<int> serverCompletedQuests;  // 게임 중 완료한 퀘스트
    public NetworkList<int> itemsInTruck;           // 실시간 수집 감지 목록

    [Header("Daily Offered Pools (Difficulty Based)")]
    public NetworkList<int> easyOffered;
    public NetworkList<int> normalOffered;
    public NetworkList<int> hardOffered;

    //서버가 보내준 클리어 리스트 보관.
    private List<int> myActuallyDoneQuests = new List<int>();

    public NetworkVariable<QuestDifficulty> selectedDifficulty = new NetworkVariable<QuestDifficulty>(
        QuestDifficulty.Easy,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Dictionary<int, List<QuestReturnPoint>> returnPointRegistry = new Dictionary<int, List<QuestReturnPoint>>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        activeQuests = new NetworkList<int>();
        serverCompletedQuests = new NetworkList<int>();
        itemsInTruck = new NetworkList<int>();
        easyOffered = new NetworkList<int>();
        normalOffered = new NetworkList<int>();
        hardOffered = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) RefreshDailyQuestPools(); // 서버 시작 시 첫 풀 생성
    }



    // [스마트폰 UI API] 개인별 퀘스트 클리어 여부 체크.
    public bool IsQuestCleared(int questID)
    {
        // 현재 수락된 퀘스트 목록에 없는 경우, 무조건 false (과거 데이터 방지)
        if (!activeQuests.Contains(questID)) return false;

        // 데이터베이스에서 퀘스트 정보 가져오기 (실패 시 조기 리턴)
        var data = GetQuestData(questID);
        if (data == null) return false;

        // 💡 로그 출력을 위해 퀘스트 이름(questName) 변수화
        string qName = data.questName;

        // 1. 서버가 공식적으로 완료 처리했거나, 즉시 클리어된 기록이 있는가?
        if (serverCompletedQuests.Contains(questID) || myActuallyDoneQuests.Contains(questID))
        {
            Debug.Log($"<color=lime>[Quest UI]</color> 퀘스트 완료: <b>[ID:{questID}] {qName}</b> (사유: 서버 기록 또는 확정 클리어)");
            return true;
        }

        // 2. 아이템 수집 타입인 경우: 트럭 안에 타겟 아이템이 들어있는가?
        if (data.type == QuestType.Collect)
        {
            if (itemsInTruck.Contains(data.targetItemID))
            {
                Debug.Log($"<color=lime>[Quest UI]</color> 퀘스트 완료: <b>[ID:{questID}] {qName}</b> (사유: 트럭 내 목표물({data.targetItemID}) 감지됨)");
                return true;
            }
        }

        // 3. 촬영 또는 기록 타입인 경우: 내 개인 앨범에 사진이 있는가?
        if (data.type == QuestType.Photo || data.type == QuestType.Record)
        {
            if (QuestCameraBridge.Instance != null && QuestCameraBridge.Instance.IsPhotoInLocalAlbum(questID))
            {
                Debug.Log($"<color=lime>[Quest UI]</color> 퀘스트 완료: <b>[ID:{questID}] {qName}</b> (사유: 개인 스마트폰 앨범에 사진 존재)");
                return true;
            }
        }

        // 미완료 상태 (너무 많이 출력될 수 있어 주석 처리. 필요시 해제하세요)
        // Debug.Log($"<color=grey>[Quest UI]</color> 진행 중: [ID:{questID}] {qName}");

        return false;
    }

    // 서버가 당사자 폰에만 클리어 여부 [체크/해제] 갱신 지시
    [Rpc(SendTo.SpecifiedInParams)]
    public void NotifyLocalClientToggleClientRpc(int questID, bool isCleared, RpcParams rpcParams)
    {
        if (isCleared) { if (!myActuallyDoneQuests.Contains(questID)) myActuallyDoneQuests.Add(questID); }
        else { if (myActuallyDoneQuests.Contains(questID)) myActuallyDoneQuests.Remove(questID); }

        Debug.Log($"<color=cyan><b>[MY QUEST]</b></color> 개인 폰 업데이트: ID {questID} -> {isCleared}");
    }

    //최종 정산시점에서 클리어 퀘스트 확정.
    public void NotifyFinalClear(int questID, ulong solverId)
    {
        if (!IsServer || !activeQuests.Contains(questID)) return;

        var data = GetQuestData(questID);
        string questName = data != null ? data.questName : "Unknown";

        if (!serverCompletedQuests.Contains(questID))
        {
            serverCompletedQuests.Add(questID);

            // 서버 진행도 트래킹 로그
            int total = activeQuests.Count;
            int cleared = serverCompletedQuests.Count;
            Debug.Log($"<color=lime><b>[SERVER MASTER]</b></color> 최종 클리어 확정: <color=white>[ID:{questID}] {questName}</color> (해결사: Client {solverId})");
            Debug.Log($"<color=lime><b>[SERVER MASTER]</b></color> 전체 진행도: {cleared}/{total}");
        }
    }

    // 환원 퀘스트 같은 '즉시 클리어' 로직용
    public void NotifyCustomQuestMet(int questID, ulong solverId)
    {
        if (!IsServer || !activeQuests.Contains(questID)) return;
        NotifyFinalClear(questID, solverId); // 마스터 장부 기록
        NotifyLocalClientToggleClientRpc(questID, true, RpcTarget.Single(solverId, RpcTargetUse.Temp)); // 폰에 체크
    }

 

    #region [생성, 수락 및 유지보수 로직]

    [ServerRpc]
    public void RefreshDailyQuestPoolsServerRpc() => RefreshDailyQuestPools();

    private void RefreshDailyQuestPools()
    {
        if (!IsServer) return;

        GenerateDifficultyPool(easyOffered, QuestDifficulty.Easy, 4);
        GenerateDifficultyPool(normalOffered, QuestDifficulty.Normal, 4);
        GenerateDifficultyPool(hardOffered, QuestDifficulty.Hard, 4);

        Debug.Log("<color=yellow>[Quest] 난이도별 4개 추출 풀 생성 완료.</color>");
    }

    private void GenerateDifficultyPool(NetworkList<int> targetList, QuestDifficulty diff, int count)
    {
        targetList.Clear();
        var pool = questDatabase.Where(q => q.difficulty == diff)
                                .OrderBy(x => Random.value)
                                .Take(count);

        foreach (var q in pool) targetList.Add(q.questID);
    }
    

    public (int money, int score) GetCalculatedQuestResults()
    {
        if (!IsServer) return (0, 0);

        int totalMoney = 0;
        int totalScore = 0;

        foreach (int qId in serverCompletedQuests)
        {
            var data = GetQuestData(qId);
            if (data == null) continue;

            // 재화 계산 (기본 보상 + 배율 + 위험 보너스)
            float totalMult = 1.0f + data.bonusMultiplier + (data.isHazardQuest ? 0.2f : 0f);
            totalMoney += Mathf.RoundToInt(data.baseReward * totalMult);

            // 실적 포인트 합산
            totalScore += data.performancePoint;
        }
        return (totalMoney, totalScore);
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
            string questListStr = "";
            foreach (int id in targetPool)
            {
                activeQuests.Add(id);
                var data = GetQuestData(id);
                questListStr += $" - [ID:{id}] {(data != null ? data.questName : "Unknown")}\n";
            }
            Debug.Log($"<color=yellow><b>[QUEST START]</b></color> {difficulty} 난이도 계약 수락! (총 {activeQuests.Count}개)\n<color=white>{questListStr}</color>");
        }
    }

    public QuestDataSO GetQuestData(int id) => questDatabase.Find(q => q.questID == id);

    public void ResetDailyQuests()
    {
        if (!IsServer) return;
        activeQuests.Clear();
        serverCompletedQuests.Clear();
        itemsInTruck.Clear(); // 💡 트럭 실시간 데이터도 싹 비워줍니다.

        RefreshDailyQuestPools();

        ResetLocalQuestsClientRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetLocalQuestsClientRpc() => myActuallyDoneQuests.Clear();

    // 포인트 등록 함수 (OnNetworkSpawn에서 호출됨)
    public void RegisterReturnPoint(int questID, QuestReturnPoint point)
    {
        if (!returnPointRegistry.ContainsKey(questID))
            returnPointRegistry[questID] = new List<QuestReturnPoint>();

        returnPointRegistry[questID].Add(point);
    }

    public void ActivateCurrentSceneReturnPoints()
    {
        foreach (var list in returnPointRegistry.Values)
            foreach (var p in list) p.SetPointActivation(false);

        foreach (int qId in activeQuests)
        {
            if (returnPointRegistry.TryGetValue(qId, out var points))
            {
                foreach (var p in points) p.SetPointActivation(true);
            }
        }
    }
    #endregion

    // ==========================================================
    // [팀원 협업용 API] 특정 기믹 퀘스트 연동 함수들
    // ==========================================================

    /// <summary>
    /// 금고 내부 스폰 포인트를 전달받아, 수집1(금고) 퀘스트가 활성화 상태면 
    /// 해당 퀘스트 아이템을 스폰하고 true를 반환합니다.
    /// </summary>
    public bool TrySetupSafeGimmick(Transform safeInsidePoint)
    {
        // 1. 아이템 스폰과 기믹 활성화 결정은 서버(방장)만 수행합니다.
        if (!IsServer) return false;

        // 2. 전달해주신 난이도별 금고 퀘스트 ID 확인
        int[] safeQuests = { 1000, 2000, 3000 }; // Easy, Normal, Hard
        int activeId = 0;

        foreach (int id in safeQuests)
        {
            if (activeQuests.Contains(id))
            {
                activeId = id;
                break;
            }
        }

        // 3. 이번 판에 금고 퀘스트가 없다면 false를 반환하여 팀원분이 기믹을 끄게 합니다.
        if (activeId == 0) return false;

        // 4. 금고 퀘스트가 있다면, 목표 아이템을 찾아 팀원분이 넘겨준 좌표에 스폰합니다!
        QuestDataSO questData = GetQuestData(activeId);
        if (questData != null && questData.targetItemID != 0)
        {
            // GameSessionManager의 GetPrefab을 활용하여 원본 프리팹 가져오기
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(questData.targetItemID);
            if (prefab != null)
            {
                // 팀원분이 만든 빈 오브젝트(safeInsidePoint)의 위치와 회전값에 스폰
                ItemBase spawned = Instantiate(prefab, safeInsidePoint.position, safeInsidePoint.rotation);

                // 네트워크 상에 생성 (모든 클라이언트에게 보임)
                spawned.GetComponent<NetworkObject>().Spawn();

                Debug.Log($"<color=lime>[Quest API]</color> 금고 연동 성공! 내부에 '{prefab.itemData.itemName}' 스폰 완료.");
                return true;
            }
        }

        return false;
    }


    //구형 로직 --------------------------------------------------------------------------------------------------------------------

    //private void Generate3Plus1(NetworkList<int> targetList, int minMain, int maxMain, int minSub, int maxSub)
    //{
    //    targetList.Clear();
    //    var main = questDatabase.Where(q => q.questLevel >= minMain && q.questLevel <= maxMain).OrderBy(x => Random.value).Take(3);
    //    var sub = questDatabase.Where(q => q.questLevel >= minSub && q.questLevel <= maxSub).OrderBy(x => Random.value).Take(1);

    //    foreach (var q in main) targetList.Add(q.questID);
    //    foreach (var q in sub) targetList.Add(q.questID);
    //}

    // 정산시 보상 계산 로직(구형)
    //public int GetCalculatedQuestReward()
    //{
    //    if (!IsServer) return 0;
    //    int questReward = 0;
    //    float totalMult = 0f;

    //    foreach (int qId in serverCompletedQuests)
    //    {
    //        var data = GetQuestData(qId);
    //        if (data == null) continue;
    //        questReward += data.baseReward;
    //        totalMult += data.bonusMultiplier;
    //        if (data.isHazardQuest) totalMult += 0.2f;
    //    }
    //    return Mathf.RoundToInt(questReward * (1.0f + totalMult));
    //}


}