using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class StartButton : NetworkBehaviour
{
    [Header("설정")]
    public string targetSceneName = "KJY_Player";

    // 씬에 배치된 SettlementZone을 드래그 앤 드롭으로 할당하거나 Awake에서 찾습니다.
    public SettlementZone shipZone;

    private void Awake()
    {
        // 만약 인스펙터에서 할당하지 않았다면 자동으로 찾음
        if (shipZone == null)
        {
            shipZone = Object.FindAnyObjectByType<SettlementZone>();
        }
    }

    /// <summary>
    /// UI Button의 OnClick 이벤트에서 이 함수를 호출하세요.
    /// </summary>
    public void OnClickStart()
    {
        // 클라이언트가 눌렀을 경우를 대비해 ServerRpc를 통해 서버에서 실행하도록 유도
        if (IsServer)
        {
            ExecuteStartLogic();
        }
        else
        {
            RequestStartServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartServerRpc()
    {
        ExecuteStartLogic();
    }

    private void ExecuteStartLogic()
    {
        Debug.Log("<color=yellow>[StartButton]</color> 게임 시작 시퀀스 가동");

        // 1. 환원 퀘스트 아이템 적재 (데이터 준비)
        PrepareReturnQuestItems();

        // 2. 구역 이동 요청 (정산 X, 씬 이름 전달)
        if (shipZone != null)
        {
            // UI 버튼 클릭이므로 호출자(PlayerInventory)는 일단 null로 전달하거나, 
            // 서버 측에서 Host의 정보를 가져오도록 처리 (ExecuteTransition 내부에서 처리됨)
            shipZone.ExecuteTransition(null, targetSceneName, false);
        }
        else
        {
            Debug.LogError("[StartButton] SettlementZone을 찾을 수 없습니다!");
        }
    }

    private void PrepareReturnQuestItems()
    {
        if (QuestManager.Instance == null || GameSessionManager.Instance == null)
        {
            Debug.LogWarning("[StartButton] 매니저 인스턴스가 없어 퀘스트 아이템을 적재할 수 없습니다.");
            return;
        }

        foreach (int qId in QuestManager.Instance.activeQuests)
        {
            var qData = QuestManager.Instance.GetQuestData(qId);

            if (qData != null && qData.type == QuestType.Return)
            {
                // 다음 씬에서 스폰될 수 있도록 대기열에 추가
                GameSessionManager.Instance.pendingSpawnItemIDs.Add(qData.targetItemID);
                Debug.Log($"<color=yellow>[Quest]</color> 환원 목표({qData.targetItemID}) 적재 완료.");
            }
        }
    }
}