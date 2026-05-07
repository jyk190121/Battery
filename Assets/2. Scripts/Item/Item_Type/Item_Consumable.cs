using UnityEngine;
using Unity.Netcode;

public class Item_Consumable : ItemBase
{
    public void Use()
    {
        // 부모의 수정된 인터페이스 호출 (인자 생략 시 default 전달됨)
        RequestUseItem();
    }

    public override void ExecuteUseItem(Vector3 direction)
    {
        // 1. 공통 실행 (애니메이션, 로그 등)
        base.ExecuteUseItem(direction);
        Debug.Log($"{itemData.itemName} 실행 (IsServer: {IsServer})");

        // 2. 서버에서만 실행되어야 하는 로직 (데이터 수정, 객체 삭제)
        if (IsServer)
        {
            HandleServerSideLogic();
        }
    }

    void HandleServerSideLogic()
    {
        // 아이템 소유자의 클라이언트 객체 탐색
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerClientId, out var client))
        {
            var pc = client.PlayerObject.GetComponent<PlayerController>();
            var targetInventory = client.PlayerObject.GetComponent<PlayerInventory>();

            if (pc != null)
            {
                // 체력 회복
                if (itemData.itemName.Equals("Hambuger"))
                {
                    pc.RestoreHealth(itemData.healAmount);
                    Debug.Log($"[서버] {pc.name} 체력 회복 완료");
                }

                // 소유자의 인벤토리에서 아이템 제거
                if (targetInventory != null)
                {
                    //targetInventory.RemoveItemByServer(itemData.itemID);

                    // 서버에서만 객체 디스폰 (에러 방지)
                    if (NetworkObject != null && NetworkObject.IsSpawned)
                    {
                        NetworkObject.Despawn();
                    }
                }
            }
        }
    }
}