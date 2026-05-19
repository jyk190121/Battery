using Unity.Netcode;
using UnityEngine;

/// <summary>
/// [무기 아이템 클래스]
/// 무기의 고유 능력치(공격력 등)만 보관하는 데이터 컨테이너 역할을 합니다.
/// 
/// </summary>

public class Item_Weapon : ItemBase
{
    [Header("Weapon Stats")]
    [Tooltip("이 무기의 기본 타격 데미지")]
    public float attackPower = 10f;

    [Header("내구도 시스템")]
    public int maxDurability = 10;

    // 멀티플레이 호환을 위해 NetworkVariable 사용 (서버만 수정 가능, 클라이언트는 읽기만 가능)
    public NetworkVariable<int> currentDurability = new NetworkVariable<int>(10, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 서버에서 초기 내구도 설정
        if (IsServer)
        {
            currentDurability.Value = maxDurability;
        }
    }

    /// <summary>
    /// 몬스터 타격 성공 시 서버에서 호출되어 내구도를 깎고 0이 되면 파괴(디스폰)합니다.
    /// </summary>
    public void DeductDurability(PlayerInventory playerInventory)
    {
        if (!IsServer) return;

        currentDurability.Value--;
        print($"[Durability] {itemData.itemName} 타격 성공! 남은 내구도: {currentDurability.Value}/{maxDurability}");
        print($"{attackPower} 데미지 줌");

        if (currentDurability.Value <= 0)
        {
            Debug.Log($"<color=red>[Weapon Destroyed]</color> {itemData.itemName}의 내구도가 전소되어 파괴됩니다.");

            // 1. 들고 있던 플레이어의 인벤토리 슬롯 비우기
            if (playerInventory != null)
            {
                playerInventory.RemoveBrokenItem(this);
            }

            // 2. Netcode 네트워크 오브젝트 디스폰 및 맵에서 완전 소멸
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }

    protected override void Start()
    {
        base.Start();
        // 무기 스폰 시 추가로 초기화할 내용이 있다면 여기에 작성합니다.
    }
}