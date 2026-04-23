using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public struct CartItemData : INetworkSerializable, IEquatable<CartItemData>
{
    public int itemID;
    public int count;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemID);
        serializer.SerializeValue(ref count);
    }
    public bool Equals(CartItemData other)
    {
        return itemID == other.itemID && count == other.count;
    }
}


public class ShopManager : NetworkBehaviour
{
    [Header("UI References")]
    public Transform cartContenParent;
    public GameObject cartItemPrefab;
    public TextMeshProUGUI totalCartPriceText;

    [Header("Economy UI")]
    public TextMeshProUGUI currentBalanceText; // 현재 보유 금액 표시 텍스트

    [Header("Feedback UI")]
    public GameObject duplicateAlertPanel;
    public float alertTime = 2f;
    private Coroutine alertCoroutine;
    public Button BuyBtn;

    [Header("Network Sync Cart")]
    public NetworkList<CartItemData> networkCartItems;

    [Header("Item Database")]
    public List<ItemDataSO> itemDatabase;

    private void Awake()
    {
        networkCartItems = new NetworkList<CartItemData>();
    }

    public override void OnNetworkSpawn()
    {
        networkCartItems.OnListChanged += (changeEvent) => { RebuildCartUI(); };
    }

    private void Start()
    {
        if (duplicateAlertPanel != null) duplicateAlertPanel.SetActive(false);
        UpdateTotalAmount();

        // 경제 매니저의 잔액이 변경될 때마다 UI를 갱신하도록 이벤트 구독
        if (GameMaster.Instance != null && GameMaster.Instance.economyManager != null)
        {
            GameMaster.Instance.economyManager.availableLoanLimit.OnValueChanged += OnBalanceChanged;
        }

        BuyBtn.onClick.AddListener(OnClickCheckoutCart);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        // 씬이 넘어가거나 파괴될 때 메모리 누수 방지를 위해 이벤트 구독 해제
        if (GameMaster.Instance != null && GameMaster.Instance.economyManager != null)
        {
            GameMaster.Instance.economyManager.availableLoanLimit.OnValueChanged -= OnBalanceChanged;
        }
    }

    // 태블릿 화면이 켜질 때마다 최신 잔액으로 갱신
    private void OnEnable()
    {
        UpdateBalanceUI();
        RebuildCartUI();
    }

    // 잔액 텍스트 갱신 함수
    public void UpdateBalanceUI()
    {
        if (currentBalanceText != null && GameMaster.Instance != null && GameMaster.Instance.economyManager != null)
        {
            int currentMoney = GameMaster.Instance.economyManager.availableLoanLimit.Value;
            currentBalanceText.text = $"보유 자금: {currentMoney} G";
        }
    }

    // 네트워크 변수가 변동될 때 호출되는 콜백 함수
    private void OnBalanceChanged(int previousValue, int newValue)
    {
        UpdateBalanceUI();
    }

    private ItemDataSO GetItemData(int id)
    {
        return itemDatabase.FirstOrDefault(i => i.itemID == id);
    }


    // ================= [네트워크 장바구니 로직] =================
    public void AddItemToCart(ItemDataSO newTargetData)
    {
        RequestAddCartItemServerRpc(newTargetData.itemID);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestAddCartItemServerRpc(int itemID)
    {
        for(int i = 0; i < networkCartItems.Count ; i++)
        {
            if(networkCartItems[i].itemID == itemID)
            {
                ShowDuplicateFeedbackClientRpc();
                return;
            }
        }

        networkCartItems.Add(new CartItemData { itemID = itemID, count = 1 });
    }

    // 아이템 수량 변경 요청 (증가/감소)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestChangeItemCountServerRpc(int itemID, int delta)
    {
        for (int i = 0; i < networkCartItems.Count; i++)
        {
            if (networkCartItems[i].itemID == itemID)
            {
                CartItemData data = networkCartItems[i];
                data.count += delta;

                if (data.count <= 0)
                {
                    networkCartItems.RemoveAt(i); // 0개면 리스트에서 제거
                }
                else
                {
                    networkCartItems[i] = data; // 구조체 업데이트
                }
                return;
            }
        }
    }


    [Rpc(SendTo.ClientsAndHost)]
    private void ShowDuplicateFeedbackClientRpc()
    {
        ShowDuplicateFeedback(); // 기존의 코루틴 경고창 함수
    }

    private void RebuildCartUI()
    {
        foreach (Transform child in cartContenParent)
        {
            Destroy(child.gameObject);
        }

        foreach(var cartItem in networkCartItems)
        {
            ItemDataSO itemData = GetItemData(cartItem.itemID);
            if (itemData != null)
            {
                GameObject newCartItem = Instantiate(cartItemPrefab, cartContenParent);
                CartItemUI cartItemUI = newCartItem.GetComponent<CartItemUI>();
                cartItemUI.Setup(itemData, this, cartItem.count);
            }
        }

        UpdateTotalAmount();
    }

    private void UpdateTotalAmount()
    {
        int totalAmount = 0;
        foreach(var cartItem in networkCartItems)
        {
            ItemDataSO itemData = GetItemData(cartItem.itemID);
            if (itemData != null)
            {
                totalAmount += itemData.basePrice * cartItem.count;
            }
        }
        
        if(totalCartPriceText != null)
        {
            totalCartPriceText.text = $"총 금액: {totalAmount} G";
        }
    }

    public void OnClickCheckoutCart()
    {
        if (networkCartItems.Count == 0) return;

        int totalPrice = 0;
        foreach(var item in networkCartItems)
        {
            ItemDataSO itemData = GetItemData(item.itemID);
            if (itemData != null)
            {
                totalPrice += itemData.basePrice * item.count;
            }
        }

        int currentMoney = GameMaster.Instance.economyManager.availableLoanLimit.Value;

        if(currentMoney >= totalPrice)
        {
            int[] itemIDs = new int[networkCartItems.Count];
            int[] counts = new int[networkCartItems.Count];

            for(int i = 0; i < networkCartItems.Count; i++)
            {
                itemIDs[i] = networkCartItems[i].itemID;
                counts[i] = networkCartItems[i].count;
            }

            ulong myClinetId = NetworkManager.Singleton.LocalClientId;
            GameMaster.Instance.RequestPurchaseServerRpc(totalPrice, itemIDs, counts, myClinetId);

            Debug.Log($"구매 요청: 총 {totalPrice} G, 아이템 수: {networkCartItems.Count}");
        }
        else
        {
            Debug.Log("잔액 부족! 구매 실패.");
        }
    }

    public void ClearCartUI()
    {
        if (IsServer)
        {
            networkCartItems.Clear();
        }
        else
        {
            RequestClearCartServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestClearCartServerRpc()
    {
        networkCartItems.Clear();
    }

    private void ShowDuplicateFeedback()
    {
        if(alertCoroutine != null)
        {
            StopCoroutine(alertCoroutine);
        }
        alertCoroutine = StartCoroutine(DuplicateAlertRoutine());
    }

    private IEnumerator DuplicateAlertRoutine()
    {
        if(duplicateAlertPanel != null)
        {
            duplicateAlertPanel.SetActive(true);
            yield return new WaitForSeconds(alertTime);
            duplicateAlertPanel.SetActive(false);
        }
    }
}