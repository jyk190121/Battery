using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 무기 아이템의 고유 능력치(공격력)와 내구도를 관리하고 파괴를 처리합니다.
/// </summary>
public class Item_Weapon : ItemBase
{
    [Header("Weapon Stats")]
    public float attackPower = 10f;

    [Header("Durability System")]
    public int maxDurability = 10;

    public NetworkVariable<int> currentDurability = new NetworkVariable<int>(
        10,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            currentDurability.Value = maxDurability;
        }
    }

    public void DeductDurability(PlayerInventory playerInventory)
    {
        if (!IsServer) { return; }

        currentDurability.Value--;

        Debug.Log($"[Durability] {itemData.itemName} 타격 성공! 남은 내구도: {currentDurability.Value}/{maxDurability}");
        Debug.Log($"{attackPower} 데미지 적용");

        if (currentDurability.Value <= 0)
        {
            Debug.Log($"<color=red>[Weapon Destroyed]</color> {itemData.itemName}의 내구도가 전소되어 파괴됩니다.");

            if (playerInventory != null)
            {
                playerInventory.RemoveBrokenItem(this);
            }

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }
}