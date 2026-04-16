using UnityEngine;

/// <summary>
/// [무기 아이템 클래스]
/// 무기의 고유 능력치(공격력 등)만 보관하는 데이터 컨테이너 역할을 합니다.
/// </summary>
public class Item_Weapon : ItemBase
{
    [Header("Weapon Stats")]
    [Tooltip("이 무기의 기본 타격 데미지")]
    public float attackPower = 10f;

    [Tooltip("무기별 넉백 수치 (필요 시 팀원과 협의하여 사용)")]
    public float knockbackForce = 5f;

    // ==========================================================
    // 💡 [주의] 
    // 공격 쿨타임, 데미지 판정 레이캐스트, RPC 통신 등의 로직은 
    // 모션과 연동되어야 하므로 이 스크립트에서는 전부 제거되었습니다.
    // 플레이어 전투 스크립트에서 아래와 같이 참조하여 사용하면 됩니다.
    // 
    // 예시: 
    // Item_Weapon equippedWeapon = twoHandedItem as Item_Weapon;
    // target.TakeDamage(equippedWeapon.attackPower);
    // ==========================================================

    protected override void Start()
    {
        base.Start();
        // 무기 스폰 시 추가로 초기화할 내용이 있다면 여기에 작성합니다.
    }
}