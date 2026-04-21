using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
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

    private Dictionary<int, CartItemUI> activeCartItems = new Dictionary<int, CartItemUI>();
    private Coroutine alertCoroutine;

    public Button BuyBtn;

    private void Start()
    {
        if (duplicateAlertPanel != null) duplicateAlertPanel.SetActive(false);
        UpdateTotalAmountUI();

        // 💡 잔액 변동 시 자동으로 텍스트를 갱신하도록 이벤트 구독 (선택 사항이지만 강력 추천)
        if (GameMaster.Instance != null && GameMaster.Instance.economyManager != null)
        {
            GameMaster.Instance.economyManager.availableLoanLimit.OnValueChanged += OnBalanceChanged;
        }

        BuyBtn.onClick.AddListener(OnClickCheckoutCart);
    }

    private void OnDestroy()
    {
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

    public void AddItemToCart(ItemDataSO newTargetData)
    {
        if (activeCartItems.ContainsKey(newTargetData.itemID))
        {
            ShowDuplicateFeedback();
            return;
        }

        GameObject newCartObj = Instantiate(cartItemPrefab, cartContenParent);
        CartItemUI cartUI = newCartObj.GetComponent<CartItemUI>();

        cartUI.Setup(newTargetData, this);
        activeCartItems.Add(newTargetData.itemID, cartUI);

        UpdateTotalAmountUI();
    }

    public void RemoveItemFromCart(int itemID)
    {
        if (activeCartItems.ContainsKey(itemID))
        {
            activeCartItems.Remove(itemID);
            UpdateTotalAmountUI();
        }
    }

    public void UpdateTotalAmountUI()
    {
        int totalPrice = activeCartItems.Values.Sum(ui => ui.itemData.basePrice * ui.currentCount);

        if (totalCartPriceText != null)
        {
            totalCartPriceText.text = $"Total: {totalPrice} G";
        }
    }

    public void OnClickCheckoutCart()
    {
        if (activeCartItems.Count == 0) return;

        int totalPrice = activeCartItems.Values.Sum(ui => ui.itemData.basePrice * ui.currentCount);

        // 구매 버튼을 눌렀을 때 내 잔액이 충분한지 로컬에서 검사
        int currentMoney = GameMaster.Instance.economyManager.availableLoanLimit.Value;

        if (currentMoney >= totalPrice)
        {
            int[] itemIDs = new int[activeCartItems.Count];
            int[] count = new int[activeCartItems.Count];
            int i = 0;

            foreach (var kv in activeCartItems)
            {
                itemIDs[i] = kv.Key;
                count[i] = kv.Value.currentCount;
                i++;
            }

            // 서버로 결제 및 배송 요청 전달
            GameSessionManager.Instance.AddItemsToSpawnQueue(itemIDs, count);

            Debug.Log($"<color=cyan>[Tablet UI]</color> 서버에 {totalPrice}G 결제 요청 완료.");
            ClearCartUI();
        }
        else
        {
            // 돈이 부족하면 결제 로직을 아예 태우지 않고 튕겨냅니다.
            Debug.LogWarning("<color=red>[Tablet UI]</color> 결제 실패: 보유 자금이 부족합니다.");
            // TODO: 여기에 "잔액 부족"이라는 경고 팝업을 띄우는 함수를 연결하세요.
        }
    }

    public void ClearCartUI()
    {
        activeCartItems.Clear();
        foreach (Transform child in cartContenParent)
        {
            Destroy(child.gameObject);
        }
        UpdateTotalAmountUI();

        UpdateBalanceUI();
    }

    private void ShowDuplicateFeedback()
    {
        if (alertCoroutine != null) StopCoroutine(alertCoroutine);
        alertCoroutine = StartCoroutine(DuplicateAlertRoutine());
    }

    private IEnumerator DuplicateAlertRoutine()
    {
        if (duplicateAlertPanel == null) yield break;

        duplicateAlertPanel.SetActive(true);
        yield return new WaitForSeconds(alertTime);
        duplicateAlertPanel.SetActive(false);
    }
}