using UnityEngine;
using Unity.Netcode;

public class MissionStartButton : NetworkBehaviour
{
    public SettlementZone shipZone;
    public string targetSceneName = "GameScene";

    public void Interact(PlayerInventory player)
    {
        if (!IsServer) return; // 씬 이동과 적재는 방장(서버)만 처리

        Debug.Log("<color=green><b>[Mission]</b> 장비를 챙겨 출발합니다! (정산 X)</color>");

        // 💡 [추가됨] 출발 직전, 수락된 환원 퀘스트의 아이템을 스폰 대기열에 추가
        if (QuestManager.Instance != null && GameSessionManager.Instance != null)
        {
            foreach (int qId in QuestManager.Instance.activeQuests)
            {
                var qData = QuestManager.Instance.GetQuestData(qId);
                if (qData != null && qData.type == QuestType.Return)
                {
                    GameSessionManager.Instance.pendingSpawnItemIDs.Add(qData.targetItemID);
                    Debug.Log($"[Quest] 환원 퀘스트 목표물({qData.targetItemID})이 스폰 대기열에 적재됨.");
                }
            }
        }

        if (shipZone != null)
        {
            shipZone.ExecuteTransition(player, targetSceneName, false);
        }
    }
}