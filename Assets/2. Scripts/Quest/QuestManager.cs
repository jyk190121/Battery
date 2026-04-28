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

    [Header("Daily Offered Pools (3+1)")]
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

        //현재 수락된 퀘스트 목록에 없는 경우, 무조건 false(과거 데이터 방지)
        if (!activeQuests.Contains(questID)) return false;

        // 1. 공식 완료거나 내 개인 장부에 있는가? (환원 등)
        if (serverCompletedQuests.Contains(questID) || myActuallyDoneQuests.Contains(questID)) return true;

        // 2. 실시간 트럭 내 수집 아이템인가?
        var data = GetQuestData(questID);
        if (data?.type == QuestType.Collect && itemsInTruck.Contains(data.targetItemID)) return true;

        // 3. 앨범에 찍어둔 사진이 있는가?
        if (QuestCameraBridge.Instance != null && QuestCameraBridge.Instance.IsPhotoInLocalAlbum(questID)) return true;

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
        Generate3Plus1(easyOffered, 1, 3, 4, 7);
        Generate3Plus1(normalOffered, 4, 7, 8, 10);
        Generate3Plus1(hardOffered, 8, 10, 4, 7);
        Debug.Log("<color=yellow>[Quest] 신규 일일 퀘스트 풀 생성 완료.</color>");
    }

    private void Generate3Plus1(NetworkList<int> targetList, int minMain, int maxMain, int minSub, int maxSub)
    {
        targetList.Clear();
        var main = questDatabase.Where(q => q.questLevel >= minMain && q.questLevel <= maxMain).OrderBy(x => Random.value).Take(3);
        var sub = questDatabase.Where(q => q.questLevel >= minSub && q.questLevel <= maxSub).OrderBy(x => Random.value).Take(1);

        foreach (var q in main) targetList.Add(q.questID);
        foreach (var q in sub) targetList.Add(q.questID);
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

    public int GetCalculatedQuestReward()
    {
        if (!IsServer) return 0;
        int questReward = 0;
        float totalMult = 0f;

        foreach (int qId in serverCompletedQuests)
        {
            var data = GetQuestData(qId);
            if (data == null) continue;
            questReward += data.baseReward;
            totalMult += data.bonusMultiplier;
            if (data.isHazardQuest) totalMult += 0.2f;
        }
        return Mathf.RoundToInt(questReward * (1.0f + totalMult));
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
}