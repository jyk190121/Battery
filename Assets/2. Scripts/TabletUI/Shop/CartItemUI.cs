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

    public void Setup(ItemDataSO data, ShopManager shopManager, int networkCount)
    {
        itemData = data;
        this.shopManager = shopManager;
        currentCount = networkCount;

        UpdateUI();
    }

    public void OnClickPlus()
    {
        shopManager.RequestChangeItemCountServerRpc(itemData.itemID, 1);
    }

    public void OnClickMinus()
    {
        shopManager.RequestChangeItemCountServerRpc(itemData.itemID, -1);  
    }

    private void UpdateUI()
    {
        itemName.text = itemData.itemName;
        count.text = currentCount.ToString();
        price.text = (itemData.basePrice * currentCount).ToString();
    }
}