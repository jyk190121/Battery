
using UnityEngine;
public class Item_Consumable : ItemBase
{
    public void Use()
    {
        Debug.Log($"{itemData.itemName}을 사용했습니다!");
        RequestDespawn(); // 사용 후 삭제
    }
}