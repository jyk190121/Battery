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

    [Header("Daily Offered Pools (3+1)")]
    public NetworkList<int> easyOffered;
    public NetworkList<int> normalOffered;
    public NetworkList<int> hardOffered;

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
        easyOffered = new NetworkList<int>();
        normalOffered = new NetworkList<int>();
        hardOffered = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) RefreshDailyQuestPools(); // 서버 시작 시 첫 풀 생성
    }

    #region [생성 및 수락 로직]

    // 💡 다음 날을 위해 모든 난이도의 3+1 풀을 새로 생성
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

    //  출발 전까지 자유롭게 난이도를 갈아탈 수 있는 로직
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AcceptDifficultyContractServerRpc(QuestDifficulty difficulty)
    {
        activeQuests.Clear(); // 이전 선택 삭제 (Overwrite)

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

            //현재 수락한 4개의 퀘스트 목록 출력
            Debug.Log($"<color=yellow><b>[QUEST START]</b></color> {difficulty} 난이도 계약 수락! (총 {activeQuests.Count}개)\n<color=white>{questListStr}</color>");
        }
    }
    #endregion

    #region [판정 및 정산 로직]

    // QuestManager.cs 의 NotifyItemCollected 수정

    public void NotifyItemCollected(int itemID, ulong solverId)
    {
        if (!IsServer) return;

        Debug.Log($"<color=cyan><b>[Quest 매니저]</b></color> 수집 보고 수신: ID {itemID} (제출자: {solverId}). 활성 퀘스트 대조 시작...");

        bool isMatched = false;
        foreach (int qId in activeQuests)
        {
            var data = GetQuestData(qId);
            if (data != null && data.type == QuestType.Collect && data.targetItemID == itemID)
            {
                Debug.Log($"<color=lime><b>[Quest 매치 성공!]</b></color> 현재 활성 퀘스트 [{data.questName}]의 목표 아이템과 일치합니다.");
                CompleteQuest(qId, solverId);
                isMatched = true;
                break; // 1:1 구조이므로 찾으면 중단
            }
        }

        if (!isMatched)
        {
            Debug.LogWarning($"<color=orange><b>[Quest 매치 실패]</b></color> ID {itemID} 아이템이 감지되었으나, 현재 수락한 수집 퀘스트 목록에 해당 아이템을 요구하는 퀘스트가 없습니다.");
        }
    }

    public void NotifyCustomQuestMet(int questID, ulong solverId)
    {
        if (!IsServer || !activeQuests.Contains(questID)) return;
        CompleteQuest(questID, solverId);
    }

  
    private void CompleteQuest(int questID, ulong solverId)
    {
        if (!IsServer) return;

        var data = GetQuestData(questID);
        string questName = data != null ? data.questName : "Unknown";

        if (!serverCompletedQuests.Contains(questID))
        {
            serverCompletedQuests.Add(questID);

            //  서버 진행도 및 잔여 퀘스트 트래킹
            int total = activeQuests.Count;
            int cleared = serverCompletedQuests.Count;

            // 아직 클리어하지 않은 남은 퀘스트 찾기
            string remainingQuests = "";
            foreach (int id in activeQuests)
            {
                if (!serverCompletedQuests.Contains(id)) remainingQuests += $"[{id}] ";
            }
            if (string.IsNullOrEmpty(remainingQuests)) remainingQuests = "없음 (ALL CLEAR!)";

            Debug.Log($"<color=lime><b>[SERVER MASTER]</b></color> 퀘스트 클리어: <color=white>[ID:{questID}] {questName}</color> (해결사: Client {solverId})");
            Debug.Log($"<color=lime><b>[SERVER MASTER]</b></color> 전체 진행도: {cleared}/{total} | 남은 퀘스트: <color=orange>{remainingQuests}</color>");
            Debug.Log($"<color=yellow><b>[REWARD]</b></color> 누적 예상 보상: {GetCalculatedQuestReward()}원");
        }

        // 당사자에게만 클리어 통보
        NotifyLocalClientClearClientRpc(questID, RpcTarget.Single(solverId, RpcTargetUse.Temp));
    }

   
    [Rpc(SendTo.SpecifiedInParams)]
    private void NotifyLocalClientClearClientRpc(int questID, RpcParams rpcParams)
    {
        if (!myActuallyDoneQuests.Contains(questID))
            myActuallyDoneQuests.Add(questID);

        var data = GetQuestData(questID);
        string questName = data != null ? data.questName : "Unknown";

        // 💡 [디버그 로그] 개인 클라이언트 패킷 수신 확인
        int myClearedCount = myActuallyDoneQuests.Count;
        int total = activeQuests.Count;

        Debug.Log($"<color=cyan><b>[MY QUEST]</b></color> 🎉 서버로부터 클리어 통보 수신: <color=white>[ID:{questID}] {questName}</color>");
        Debug.Log($"<color=cyan><b>[MY QUEST]</b></color> 나의 기여도(진행도): {myClearedCount}/{total}");
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

    // [수정] 정산/전멸 시 호출: 데이터를 비우고 '다음 날' 풀을 즉시 생성
    public void ResetDailyQuests()
    {
        if (!IsServer) return;
        activeQuests.Clear();
        serverCompletedQuests.Clear();

        RefreshDailyQuestPools(); // 다음 날을 위한 신규 3+1 생성

        ResetLocalQuestsClientRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetLocalQuestsClientRpc() => myActuallyDoneQuests.Clear();
    #endregion


    // 포인트 등록 함수 (OnNetworkSpawn에서 호출됨)
    public void RegisterReturnPoint(int questID, QuestReturnPoint point)
    {
        if (!returnPointRegistry.ContainsKey(questID))
            returnPointRegistry[questID] = new List<QuestReturnPoint>();

        returnPointRegistry[questID].Add(point);
    }

    public void ActivateCurrentSceneReturnPoints()
    {
        // 1. 장부의 모든 포인트를 일단 잠재움
        foreach (var list in returnPointRegistry.Values)
            foreach (var p in list) p.SetPointActivation(false);

        // 2. 현재 활성화된 퀘스트(activeQuests)에 해당하는 포인트만 깨움
        foreach (int qId in activeQuests)
        {
            if (returnPointRegistry.TryGetValue(qId, out var points))
            {
                foreach (var p in points) p.SetPointActivation(true);
            }
        }
    }


}