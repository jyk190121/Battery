using UnityEngine;
using Unity.Netcode;

public class QuestReturnPoint : NetworkBehaviour
{
    [Header("Quest Settings")]
    public int targetQuestID;    // 연동된 퀘스트 ID
    public int requiredItemID;   // 반납해야 할 물건의 ID

    [Header("Visual Components")]
    [Tooltip("반납 전 보여줄 투명한 가이드(큐브 등)")]
    public GameObject ghostModel;

    [Tooltip("반납 성공 시 활성화될(True) 실제 물건 모델")]
    public GameObject realModel;

    // 서버가 관리하며 모든 클라이언트에게 '반납 완료' 상태를 동기화함
    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        // 처음 나타날 때와 상태가 변할 때 시각적 동기화
        RefreshState(isCompleted.Value);
        isCompleted.OnValueChanged += (prev, next) => RefreshState(next);
    }

    // PlayerInventory에서 바라보고 E키를 누르면 호출됨
    public void Interact(PlayerInventory player)
    {
        // 이미 완료된 경우 상호작용 차단(Lock)
        if (isCompleted.Value) return;

        // 서버에 인벤토리 확인 및 반납 요청
        TryReturnServerRpc(player.OwnerClientId);
    }

    [Rpc(SendTo.Server)]
    private void TryReturnServerRpc(ulong clientId)
    {
        if (isCompleted.Value) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(clientId, out NetworkObject playerObj))
        {
            PlayerInventory inventory = playerObj.GetComponent<PlayerInventory>();

            // 인벤토리에 목표 아이템 ID가 있는지 확인
            if (inventory != null && inventory.HasItem(requiredItemID))
            {
                // 1. 아이템 제거
                inventory.RemoveItemByServer(requiredItemID);

                // 2. 완료 상태로 전환 (자동으로 전원 시각적 변경 및 Lock 발생)
                isCompleted.Value = true;

                // 3. 퀘스트 매니저 통보
                QuestManager.Instance.NotifyCustomQuestMet(targetQuestID, clientId);

                Debug.Log($"<color=cyan>[Quest]</color> {targetQuestID}번 의뢰물 반납 및 완료 처리.");
            }
        }
    }

    private void RefreshState(bool completed)
    {
        if (realModel != null) realModel.SetActive(completed);   // 완료 시 실물 등장
        if (ghostModel != null) ghostModel.SetActive(!completed); // 완료 시 가이드 퇴장

        // 완료 시 콜라이더를 꺼서 더 이상 레이캐스트에 잡히지 않게 함 (Lock)
        if (completed)
        {
            if (TryGetComponent(out Collider col)) col.enabled = false;
            // 외곽선이 켜져 있었다면 강제로 끔
            Outline outline = GetComponentInChildren<Outline>();
            if (outline != null) outline.enabled = false;
        }
    }
}