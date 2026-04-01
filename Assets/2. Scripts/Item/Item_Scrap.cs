using UnityEngine;

public class Item_Scrap : ItemBase
{
    [Header("Scrap Info")]
    public int currentScrapValue; // 현재 가격 (SO 기본값에서 랜덤하게 변동 가능)

    private void Start()
    {
        if (currentScrapValue == 0 && itemData != null)
            currentScrapValue = itemData.basePrice;
    }
}