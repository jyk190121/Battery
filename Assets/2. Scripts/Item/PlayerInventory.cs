using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Slots")]
    public ItemBase[] slots = new ItemBase[4];
    public int currentSlotIndex = 0;
    public ItemBase twoHandedItem = null;

    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask itemLayer;

    [Header("Hand Transforms")]
    public Transform leftHandTransform;
    public Transform bothHandsTransform;

    [Header("Drop Settings")]
    public float throwForce = 7f; // 기본 던지는 힘

    [Header("Status")]
    public float currentWeightPenalty = 1.0f;

    [Header("Events")]
    public Action<int> OnSlotChanged;
    public Action OnInventoryUpdated;
    public Action<bool> OnTwoHandedToggled;

    private ItemBase lastLookedItem;

    void Update()
    {
        CheckInteraction();
        HandleSlotChange();

        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame) TryPickUpAction();
            if (Keyboard.current[Key.G].wasPressedThisFrame) RequestDropCurrentItem();
        }
    }

    // ==========================================================
    // 1. 조준 및 하이라이트 제어
    // ==========================================================
    private void CheckInteraction()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // 진단용 코드를 걷어내고 레이어 마스크를 사용하는 최적화 코드로 복구
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, itemLayer))
        {
            if (hit.collider.TryGetComponent(out ItemBase targetItem))
            {
                if (lastLookedItem != targetItem)
                {
                    ClearHighlight();
                    lastLookedItem = targetItem;
                    if (lastLookedItem.TryGetComponent(out Outline outline)) outline.enabled = true;

                    // HUD 갱신 (추후 UI 연결 시 주석 해제)
                    // InteractionUI.Instance.Show(targetItem.itemData.itemName);
                }
                return;
            }
        }
        ClearHighlight();
    }

    private void ClearHighlight()
    {
        if (lastLookedItem != null)
        {
            if (lastLookedItem.TryGetComponent(out Outline outline)) outline.enabled = false;
            // InteractionUI.Instance.Hide();
            lastLookedItem = null;
        }
    }

    // ==========================================================
    // 2. 아이템 습득 로직
    // ==========================================================
    private void TryPickUpAction()
    {
        if (lastLookedItem != null) LocalPickUpLogic(lastLookedItem);
    }

    private void LocalPickUpLogic(ItemBase targetItem)
    {
        // 줍는 순간 하이라이트 및 조준 정보 강제 초기화
        if (targetItem.TryGetComponent(out Outline outline)) outline.enabled = false;
        if (lastLookedItem == targetItem)
        {
            lastLookedItem = null;
            // InteractionUI.Instance.Hide(); 
        }

        // 양손 장비 처리
        if (targetItem.itemData.handType == HandType.TwoHand)
        {
            if (twoHandedItem != null) return;
            if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(false);

            twoHandedItem = targetItem;
            targetItem.RequestChangeOwnership(true, bothHandsTransform);
            currentWeightPenalty = 0.7f;
            OnTwoHandedToggled?.Invoke(true);
        }
        // 한손 장비 처리 (현재 슬롯 -> 빈 슬롯 -> 스왑)
        else
        {
            if (twoHandedItem != null) return;

            int targetSlot = -1;
            if (slots[currentSlotIndex] == null) targetSlot = currentSlotIndex;
            else
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) { targetSlot = i; break; }
                }
            }

            if (targetSlot == -1)
            {
                RequestDropCurrentItem();
                targetSlot = currentSlotIndex;
            }

            slots[targetSlot] = targetItem;
            targetItem.RequestChangeOwnership(true, leftHandTransform);

            if (targetSlot != currentSlotIndex) targetItem.gameObject.SetActive(false);
        }

        OnInventoryUpdated?.Invoke();
    }

    // ==========================================================
    // 3. 아이템 투척 로직 (정교한 투척 적용)
    // ==========================================================
    public void RequestDropCurrentItem()
    {
        ItemBase itemToDrop = null;

        if (twoHandedItem != null)
        {
            itemToDrop = twoHandedItem;
            twoHandedItem = null;
            currentWeightPenalty = 1.0f;
            OnTwoHandedToggled?.Invoke(false);
        }
        else if (slots[currentSlotIndex] != null)
        {
            itemToDrop = slots[currentSlotIndex];
            slots[currentSlotIndex] = null;
        }

        if (itemToDrop != null)
        {
            // 투척 전 초기화
            if (itemToDrop.TryGetComponent(out Outline outline)) outline.enabled = false;
            if (lastLookedItem == itemToDrop) lastLookedItem = null;

            itemToDrop.RequestChangeOwnership(false, null);

            // 카메라 정면을 기준으로 투척 위치 산정
            Vector3 throwOrigin = Camera.main != null ? Camera.main.transform.position : transform.position;
            Vector3 throwDir = Camera.main != null ? Camera.main.transform.forward : transform.forward;

            itemToDrop.transform.position = throwOrigin + throwDir * 1.0f;
            itemToDrop.transform.rotation = Quaternion.identity; // 회전 정자세로 초기화

            // 물리 연산: 포물선 투척
            if (itemToDrop.TryGetComponent(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                Vector3 forceDir = (throwDir + Vector3.up * 0.2f).normalized; // 살짝 위로 던지기
                rb.AddForce(forceDir * throwForce, ForceMode.Impulse);

                // [중요] 아이템 측에 던져졌음을 알림
                itemToDrop.BeginThrownState();
            }

            // 양손템 투척 시 숨겨둔 한손템 복구
            if (twoHandedItem == null && slots[currentSlotIndex] != null)
                slots[currentSlotIndex].gameObject.SetActive(true);

            OnInventoryUpdated?.Invoke();
        }
    }

    // ==========================================================
    // 4. 슬롯 변경 로직 (조건문 최적화)
    // ==========================================================
    private void HandleSlotChange()
    {
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (scroll == 0f) return;

        int prevIndex = currentSlotIndex;

        if (scroll < 0f && currentSlotIndex < slots.Length - 1) currentSlotIndex++;
        else if (scroll > 0f && currentSlotIndex > 0) currentSlotIndex--;

        if (prevIndex != currentSlotIndex)
        {
            if (twoHandedItem == null && slots[prevIndex] != null) slots[prevIndex].gameObject.SetActive(false);
            if (twoHandedItem == null && slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }
}