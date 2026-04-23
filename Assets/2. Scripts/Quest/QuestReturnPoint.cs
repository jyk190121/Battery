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

    // [동기화 데이터] 모든 클라이언트가 실시간으로 공유받는 변수들
    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isActivatedByManager = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.RegisterReturnPoint(targetQuestID, this);

        // 데이터가 바뀌면 (서버가 바꾸면) 모든 클라이언트에서 화면을 새로고침함
        RefreshState(isCompleted.Value);
        isCompleted.OnValueChanged += (prev, next) => RefreshState(next);
        isActivatedByManager.OnValueChanged += (prev, next) => RefreshState(isCompleted.Value);
    }

    public void SetPointActivation(bool isActive)
    {
        // 서버만 이 값을 바꿀 수 있고, 바꾸는 순간 모든 클라이언트에 전파됩니다.
        if (IsServer) isActivatedByManager.Value = isActive;
    }

    public bool IsInteractable()
    {
        return isActivatedByManager.Value && !isCompleted.Value;
    }

    public void Interact(PlayerInventory player)
    {
        if (!IsInteractable()) return;

        // 손에 든 게 정답이 아니면 여기서 컷!
        ItemBase held = player.HeldItem;
        if (held == null || held.itemData.itemID != requiredItemID)
        {
            Debug.Log($"<color=orange>[Quest] {requiredItemID}번 아이템을 손에 들어야 작동합니다!</color>");
            return;
        }

        // 손에 든 게 확실할 때만 서버에 보고
        TryReturnServerRpc(player.OwnerClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryReturnServerRpc(ulong clientId)
    {
        if (isCompleted.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv != null)
            {
                //  RemoveItemByServer가 true(삭제 성공)를 반환할 때만 진행!
                if (inv.RemoveItemByServer(requiredItemID))
                {
                    isCompleted.Value = true;
                    QuestManager.Instance.NotifyCustomQuestMet(targetQuestID, clientId);
                }
                else
                {
                    Debug.LogWarning($"<color=red>[Security]</color> Client {clientId}의 아이템 삭제 실패. 퀘스트 클리어 거부됨.");
                }
            }
        }
    }

    private void RefreshState(bool completed)
    {
        // 기획자님이 인스펙터 슬롯에 꽂아준 '진짜 내용물'을 켜줌
        if (realModel != null) realModel.SetActive(completed);
        if (ghostModel != null) ghostModel.SetActive(isActivatedByManager.Value && !completed);

        if (completed)
        {
            if (TryGetComponent(out Collider col)) col.enabled = false;
            if (outline != null) outline.enabled = false;
        }
    }
}