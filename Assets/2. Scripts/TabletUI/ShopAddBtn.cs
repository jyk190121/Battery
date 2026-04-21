using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public class ShopItemMapping
{
    public Button buyButton;      // 왼쪽 리스트의 Add 버튼
    public ItemDataSO itemData;   // 그 버튼이 눌렸을 때 장바구니에 담길 아이템 데이터
}

public class ShopAddBtn : MonoBehaviour
{
    public ShopManager shopManager;
    public List<ShopItemMapping> shopItems;

    private void Start()
    {
        // 시작할 때 모든 Add 버튼에 자동으로 클릭 이벤트를 달아줍니다.
        foreach (var mapping in shopItems)
        {
            if (mapping.buyButton != null && mapping.itemData != null)
            {
                mapping.buyButton.onClick.AddListener(() => shopManager.AddItemToCart(mapping.itemData));
            }
        }
    }
}