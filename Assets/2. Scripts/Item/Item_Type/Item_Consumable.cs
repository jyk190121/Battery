using UnityEngine;

public class Item_Consumable : ItemBase
{
    public void Use()
    {
        // 부모의 수정된 인터페이스 호출 (인자 생략 시 default 전달됨)
        RequestUseItem();
    }

    public override void ExecuteUseItem(Vector3 direction)
    {
        base.ExecuteUseItem(direction);

        Debug.Log($"{itemData.itemName}을(를) 사용했습니다!");

        if (IsOwner)
        {
            PlayerInventory.LocalInstance.RemoveItemByServer(itemData.itemID);
        }
    }
}