using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 소비형 아이템(햄버거, 주사기, 배터리 등)의 사용 로직과 서버 측 데이터 적용을 담당.
/// </summary>
public class Item_Consumable : ItemBase
{
    public void Use()
    {
        RequestUseItem();
    }

    public override void ExecuteUseItem(Vector3 direction)
    {
        base.ExecuteUseItem(direction);
        Debug.Log($"{itemData.itemName} 실행 (IsServer: {IsServer})");

        if (IsOwner && itemData.itemName.Equals("Syringe"))
        {
            if (NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent(out PlayerMove playerMove))
            {
                playerMove.ApplySpeedBuff(1.5f, 3f);
            }
        }

        if (IsServer)
        {
            HandleServerSideLogic();
        }
    }

    private void HandleServerSideLogic()
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerClientId, out NetworkClient client))
        {
            PlayerController playerController = client.PlayerObject.GetComponent<PlayerController>();
            PlayerInventory targetInventory = client.PlayerObject.GetComponent<PlayerInventory>();

            if (playerController != null)
            {
                if (itemData.itemName.Equals("Hambuger"))
                {
                    playerController.RestoreHealth(itemData.healAmount);
                    Debug.Log($"[서버] {playerController.name} 체력 회복 완료");
                }
                else if (itemData.itemName.Equals("Battery"))
                {
                    PhoneBatteryController.Instance.RechargeBattery();
                    Debug.Log("휴대폰 충전 완료!");
                }
                else if (itemData.itemName.Equals("Syringe"))
                {
                    Debug.Log("주사기 아이템 사용");
                }

                if (targetInventory != null)
                {
                    if (NetworkObject != null && NetworkObject.IsSpawned)
                    {
                        NetworkObject.Despawn();
                    }
                }
            }
        }
    }
}