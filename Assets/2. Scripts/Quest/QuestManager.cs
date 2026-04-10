using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance;

    public List<QuestDataSO> questDatabase;
    public NetworkList<int> activeQuests;
    public NetworkList<int> serverCompletedQuests;

    private List<int> myActuallyDoneQuests = new List<int>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);

        activeQuests = new NetworkList<int>();
        serverCompletedQuests = new NetworkList<int>();
    }

    // 💡 [추가됨] UI 태블릿에서 퀘스트 '수락' 시 호출
    public void AcceptQuestFromTablet(int questID)
    {
        AcceptQuestServerRpc(questID);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AcceptQuestServerRpc(int questID)
    {
        if (!activeQuests.Contains(questID))
        {
            activeQuests.Add(questID);
            Debug.Log($"[Quest] {questID}번 퀘스트가 수락되어 활성화되었습니다!");
        }
    }

    public void NotifyItemCollected(int itemID, ulong solverId)
    {
        if (!IsServer) return;
        foreach (int qId in activeQuests)
        {
            var data = GetQuestData(qId);
            if (data != null && data.type == QuestType.Collect && data.targetItemID == itemID)
            {
                CompleteQuest(qId, solverId);
            }
        }
    }

    public void NotifyCustomQuestMet(int questID, ulong solverId)
    {
        if (!IsServer || !activeQuests.Contains(questID)) return;
        CompleteQuest(questID, solverId);
    }

    private void CompleteQuest(int questID, ulong solverId)
    {
        if (!serverCompletedQuests.Contains(questID))
        {
            serverCompletedQuests.Add(questID);
            Debug.Log($"<color=magenta>[Quest]</color> 퀘스트 {questID}번 최초 클리어! (서버에 기록됨)");
        }
        else
        {
            Debug.Log($"<color=orange>[Quest]</color> 퀘스트 {questID}번 중복 클리어 (보상은 1회만 적용됨)");
        }
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
        float questMultiplier = 0f;

        foreach (int qId in serverCompletedQuests)
        {
            var data = GetQuestData(qId);
            if (data == null) continue;
            questReward += data.baseReward;
            questMultiplier += data.bonusMultiplier;
            if (data.isHazardQuest) questMultiplier += 0.2f;
        }
        return Mathf.RoundToInt(questReward * (1.0f + questMultiplier));
    }

    public QuestDataSO GetQuestData(int id) => questDatabase.Find(q => q.questID == id);
}