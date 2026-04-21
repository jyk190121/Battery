using NUnit.Framework.Interfaces;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CartItemUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI itemName;
    public TextMeshProUGUI count;
    public TextMeshProUGUI price;

    public ItemDataSO itemData { get; private set; }
    public int currentCount { get; private set; } = 1;

    private ShopManager shopManager;

    public Button MinusBtn;
    public Button PlusBtn;

    public void Start()
    {
        MinusBtn.onClick.AddListener(OnClickMinus);
        PlusBtn.onClick.AddListener(OnClickPlus);
    }

    public void Setup(ItemDataSO data, ShopManager shopManager)
    {
        itemData = data;
        this.shopManager = shopManager;
        currentCount = 1;

        UpdateUI();
    }

    public void OnClickPlus()
    {
        currentCount++;
        UpdateUI();
        shopManager.UpdateTotalAmountUI();
    }

    public void OnClickMinus()
    {
        currentCount--;

        if (currentCount <= 0)
        {
            // 0개가 되면 매니저의 목록에서 지우고 프리팹 파괴
            shopManager.RemoveItemFromCart(itemData.itemID);
            Destroy(gameObject);
        }
        else
        {
            UpdateUI();
            shopManager.UpdateTotalAmountUI();
        }
    }

    private void UpdateUI()
    {
        itemName.text = itemData.itemName;
        count.text = currentCount.ToString();
        price.text = (itemData.basePrice * currentCount).ToString();
    }
}