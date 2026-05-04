using UnityEngine;
using Unity.Netcode;

public class QuestReturnPoint : NetworkBehaviour
{
    [Header("Quest Settings")]
    public int targetQuestID;
    public int requiredItemID;

    [Header("Visual Components")]
    public GameObject ghostModel;
    public GameObject realModel;
    public Outline outline;

    // [동기화 데이터] 
    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);       // 최종 클리어 (2단계 완료)
    private NetworkVariable<bool> hasItem = new NetworkVariable<bool>(false);            // 아이템 반납됨 (1단계 완료)
    private NetworkVariable<bool> isActivatedByManager = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.RegisterReturnPoint(targetQuestID, this);

        // 데이터 변경 시 화면 갱신 연결
        RefreshState(hasItem.Value, isCompleted.Value);

        hasItem.OnValueChanged += (prev, next) => RefreshState(next, isCompleted.Value);
        isCompleted.OnValueChanged += (prev, next) => RefreshState(hasItem.Value, next);
        isActivatedByManager.OnValueChanged += (prev, next) => RefreshState(hasItem.Value, isCompleted.Value);
    }

    public void SetPointActivation(bool isActive)
    {
        if (IsServer) isActivatedByManager.Value = isActive;
    }

    public bool IsInteractable()
    {
        return isActivatedByManager.Value && !isCompleted.Value;
    }

    public void Interact(PlayerInventory player)
    {
        if (!IsInteractable()) return;

        // 1단계: 아이템 반납이 아직 안 된 경우
        if (!hasItem.Value)
        {
            ItemBase held = player.HeldItem;
            if (held == null || held.itemData.itemID != requiredItemID)
            {
                Debug.Log($"<color=orange>[Quest] {requiredItemID}번 아이템을 손에 들어야 작동합니다!</color>");
                return;
            }
            // 아이템 반납 RPC 호출
            TryReturnItemServerRpc(player.OwnerClientId);
        }
        // 2단계: 아이템은 이미 있고, 다시 한번 눌러서 영혼 세계 이동 및 클리어
        else
        {
            TryFinalClearServerRpc(player.OwnerClientId);
        }
    }

    // [1단계] 아이템 반납 처리 RPC
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryReturnItemServerRpc(ulong clientId)
    {
        if (hasItem.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv != null && inv.RemoveItemByServer(requiredItemID))
            {
                hasItem.Value = true; // 아이템 반납 상태로 변경 (모델 교체됨)
                Debug.Log($"<color=cyan>[Quest]</color> Client {clientId} 아이템 반납 완료. 다음 클릭 시 영혼 세계 이동.");
            }
        }
    }

    // [2단계] 최종 상호작용 (이동 및 퀘스트 클리어) RPC
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryFinalClearServerRpc(ulong clientId)
    {
        if (isCompleted.Value || !hasItem.Value) return;

        isCompleted.Value = true; // 최종 완료

        // 퀘스트 매니저 클리어 보고
        QuestManager.Instance.NotifyCustomQuestMet(targetQuestID, clientId);

        // 이동 로그 및 연출 알림
        NotifyTeleportLogClientRpc(clientId);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyTeleportLogClientRpc(ulong clientId)
    {
        Debug.Log($"<color=purple><b>[Gimmick]</b></color> Client {clientId} 영혼 세계 이동 완료! (퀘스트 ID: {targetQuestID})");

        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            // 실제 이동 코드는 이후 팀원 작업 시 여기에 추가
        }
    }

    private void RefreshState(bool itemReturned, bool completed)
    {
        // 1단계(반납)가 완료되면 실제 모델을 보여줌
        if (realModel != null) realModel.SetActive(itemReturned);

        // 아이템 반납 전이고 매니저가 활성화했을 때만 고스트 모델 보여줌
        if (ghostModel != null) ghostModel.SetActive(isActivatedByManager.Value && !itemReturned);

        // 최종 2단계까지 끝나면 콜라이더와 아웃라인 제거
        if (completed)
        {
            if (TryGetComponent(out Collider col)) col.enabled = false;
            if (outline != null) outline.enabled = false;
        }
    }
}