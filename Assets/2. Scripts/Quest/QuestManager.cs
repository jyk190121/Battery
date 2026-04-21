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

    // 💡 [수정] 출발 전까지 자유롭게 난이도를 갈아탈 수 있는 로직
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

        if (targetPool != null)
        {
            foreach (int id in targetPool) activeQuests.Add(id);
            Debug.Log($"[Quest] {difficulty} 계약으로 변경됨.");
        }
    }
    #endregion

    #region [판정 및 정산 로직]

    public void NotifyItemCollected(int itemID, ulong solverId)
    {
        if (!IsServer) return;
        foreach (int qId in activeQuests)
        {
            var data = GetQuestData(qId);
            if (data != null && data.type == QuestType.Collect && data.targetItemID == itemID)
                CompleteQuest(qId, solverId);
        }
    }

    public void NotifyCustomQuestMet(int questID, ulong solverId)
    {
        if (!IsServer || !activeQuests.Contains(questID)) return;
        CompleteQuest(questID, solverId);
    }

    private void CompleteQuest(int questID, ulong solverId)
    {
        if (!serverCompletedQuests.Contains(questID)) serverCompletedQuests.Add(questID);
        NotifyLocalClientClearClientRpc(questID, RpcTarget.Single(solverId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void NotifyLocalClientClearClientRpc(int questID, RpcParams rpcParams)
    {
        if (!myActuallyDoneQuests.Contains(questID)) myActuallyDoneQuests.Add(questID);
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

    // 💡 [수정] 정산/전멸 시 호출: 데이터를 비우고 '다음 날' 풀을 즉시 생성
    public void ResetDailyQuests()
    {
        if (!IsServer) return;
        activeQuests.Clear();
        serverCompletedQuests.Clear();

        RefreshDailyQuestPools(); // 👈 다음 날을 위한 신규 3+1 생성

        ResetLocalQuestsClientRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetLocalQuestsClientRpc() => myActuallyDoneQuests.Clear();
    #endregion
}