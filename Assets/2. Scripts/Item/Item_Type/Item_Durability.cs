using UnityEngine;

public class Item_Durability : ItemBase
{
    public float currentDurability = 100f;

    // 테스트용: 던질 때마다 내구도 감소
    public override void BeginThrownState()
    {
        base.BeginThrownState();
        currentDurability = Mathf.Max(0, currentDurability - 10f);
        Debug.Log($"<color=green>[Durability]</color> {itemData.itemName} 내구도 감소: {currentDurability}");
    }
}