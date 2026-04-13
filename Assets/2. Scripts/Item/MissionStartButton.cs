using UnityEngine;
using Unity.Netcode;

public class MissionStartButton : NetworkBehaviour
{
    public SettlementZone shipZone;
    public string targetSceneName = "GameScene";

    public void Interact(PlayerInventory player)
    {
       
        // 1. 환원 퀘스트 템 적재 (데이터 준비)
        PrepareReturnQuestItems();

        // 2. 구역에 이동 요청 (정산 X, 씬 이름 전달)
        if (shipZone != null)
        {
            shipZone.ExecuteTransition(player, targetSceneName, false);
        }
    }
    private void PrepareReturnQuestItems()
    {
        // 매니저들이 씬에 제대로 있는지 안전 검사
        if (QuestManager.Instance == null || GameSessionManager.Instance == null) return;

        // 현재 진행 중인 모든 퀘스트를 순회
        foreach (int qId in QuestManager.Instance.activeQuests)
        {
            var qData = QuestManager.Instance.GetQuestData(qId);

            // 퀘스트가 존재하고, 그 타입이 '환원(Return)'일 경우에만 실행
            if (qData != null && qData.type == QuestType.Return)
            {
                // 다음 씬(게임 씬)에서 스폰될 수 있도록 대기열에 아이템 ID 추가
                GameSessionManager.Instance.pendingSpawnItemIDs.Add(qData.targetItemID);
                Debug.Log($"<color=yellow>[Quest]</color> 환원 퀘스트(ID:{qId}) 목표물({qData.targetItemID}) 적재 완료.");
            }
        }
    }
}