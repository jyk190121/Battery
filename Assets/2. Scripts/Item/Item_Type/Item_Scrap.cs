using UnityEngine;

public class Item_Scrap : ItemBase
{
    public int currentScrapValue;

    // [경고 해결] 상속 규칙 준수를 위해 override 명시 및 base.Start() 호출
    protected override void Start()
    {
        base.Start();
        if (currentScrapValue == 0 && itemData != null) currentScrapValue = itemData.basePrice;
    }
}