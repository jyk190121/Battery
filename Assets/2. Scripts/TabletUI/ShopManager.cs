using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class ShopManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform cartContenParent;
    public GameObject cartItemPrefab;
    public TextMeshProUGUI totalCartPriceText;

    [Header("Feedback UI")]
    public GameObject duplicateAlertPanel; // 인스펙터에서 경고 패널 연결
    public float alertTime = 2f;

    private Dictionary<int, CartItemUI> activeCartItems = new Dictionary<int, CartItemUI>();
    private Coroutine alertCoroutine;

    private void Start()
    {
        if (duplicateAlertPanel != null) duplicateAlertPanel.SetActive(false);
        UpdateTotalAmountUI();
    }

    // 왼쪽 상점 리스트에서 [Add] 버튼 호출 시
    public void AddItemToCart(ItemDataSO newTargetData)
    {
        // 중복 감지 시 ShopManager가 직접 경고 패널을 띄움
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
        if(activeCartItems.ContainsKey(itemID))
        {
            activeCartItems.Remove(itemID);
            UpdateTotalAmountUI();
        }
    }

    // UI 갱신
    public void UpdateTotalAmountUI()
    {
        int totalPrice = activeCartItems.Values.Sum(ui => ui.itemData.basePrice * ui.currentCount);

        if(totalCartPriceText != null)
        {
            totalCartPriceText.text = $"{totalPrice}";
        }
    }

    // 최종 결제 버튼
    public void OnClickCheckoutCart()
    {
        if (activeCartItems.Count == 0) return;

        int totalPrice = activeCartItems.Values.Sum(ui => ui.itemData.basePrice * ui.currentCount);

        // GameMaster를 통해 EconomyManager의 TryPurchaseWithLoan 결과값 반환
        bool isPurchased = GameMaster.Instance != null && GameMaster.Instance.RequestPurchase(totalPrice);

        if (isPurchased)
        {
            int[] itemIDs = new int[activeCartItems.Count];
            int[] count = new int[activeCartItems.Count];
            int i = 0;

            foreach(var kv in activeCartItems)
            {
                itemIDs[i] = kv.Key;
                count[i] = kv.Value.currentCount;
            }

            GameSessionManager.Instance.AddItemToSpawnQueue(itemIDs, count);

            Debug.Log($"<color=cyan>[Tablet UI]</color> 결제 완료! 총 {totalPrice}G 차감.");
            ClearCartUI();
        }
        else
        {
            Debug.LogWarning("<color=red>[Tablet UI]</color> 결제 실패: 잔액이 부족합니다.");
        }
    }

    // 데이터 및 UI 완전 초기화
    public void ClearCartUI()
    {
        activeCartItems.Clear();
        foreach (Transform child in cartContenParent)
        {
            Destroy(child.gameObject);
        }
        UpdateTotalAmountUI();
    }

    // 중복 경고 코루틴
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
