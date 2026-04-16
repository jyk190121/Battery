using UnityEngine;

public class Item_Consumable : ItemBase
{
    public void Use()
    {
        // 부모의 수정된 인터페이스 호출 (인자 생략 시 default 전달됨)
        RequestUseItem();
    }

    // 부모와 동일하게 Vector3 direction 매개변수 추가
    public override void ExecuteUseItem(Vector3 direction)
    {
        // 부모 메서드 호출 시에도 인자 전달
        base.ExecuteUseItem(direction);

        Debug.Log($"{itemData.itemName}을(를) 사용했습니다!");

        if (IsOwner)
        {
            RequestDespawn();
        }
    }
}