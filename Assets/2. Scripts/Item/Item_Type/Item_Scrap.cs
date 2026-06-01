using UnityEngine;

/// <summary>
/// 폐지(Scrap) 아이템입니다. 정산 시 환산될 가치를 관리.
/// </summary>
public class Item_Scrap : ItemBase
{
    public int currentScrapValue;

    protected override void Start()
    {
        base.Start();

        if (currentScrapValue == 0 && itemData != null)
        {
            currentScrapValue = itemData.basePrice;
        }
    }
}