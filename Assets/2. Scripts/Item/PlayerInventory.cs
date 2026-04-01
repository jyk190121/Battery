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

    public Transform leftHandTransform;
    public Transform bothHandsTransform;
    public float throwForce = 7f;

    public Action<int> OnSlotChanged;
    public Action OnInventoryUpdated;
    public Action<bool> OnTwoHandedToggled;

    private ItemBase lastLookedItem;
    private DepartureButton lastLookedButton; // 버튼 조준용 변수 추가

    void Start()
    {
        LoadInventoryData(); // 씬 로드 시 인벤토리 복구
    }

    void Update()
    {
        CheckInteraction();
        HandleSlotChange();

        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                if (lastLookedItem != null) TryPickUpAction();
                else if (lastLookedButton != null) lastLookedButton.Interact(this);
            }
            if (Keyboard.current[Key.G].wasPressedThisFrame) RequestDropCurrentItem();
        }
    }

    private void CheckInteraction()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, itemLayer))
        {
            if (hit.collider.TryGetComponent(out ItemBase targetItem))
            {
                if (lastLookedItem != targetItem)
                {
                    ClearHighlight();
                    lastLookedItem = targetItem;
                    Debug.Log($"포커스: {targetItem.itemData.itemName}");
                    if (lastLookedItem.TryGetComponent(out Outline outline)) outline.enabled = true;
                }
                return;
            }
            if (hit.collider.TryGetComponent(out DepartureButton targetButton))
            {
                if (lastLookedButton != targetButton)
                {
                    ClearHighlight();
                    lastLookedButton = targetButton;
                    Debug.Log("<color=magenta>이륙 버튼 조준됨.</color>");
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
            lastLookedItem = null;
        }
        lastLookedButton = null;
    }

    private void TryPickUpAction() { if (lastLookedItem != null) LocalPickUpLogic(lastLookedItem); }

    private void LocalPickUpLogic(ItemBase targetItem)
    {
        if (targetItem.TryGetComponent(out Outline outline)) outline.enabled = false;
        lastLookedItem = null;

        if (targetItem.itemData.handType == HandType.TwoHand)
        {
            if (twoHandedItem != null) return;
            if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(false);
            twoHandedItem = targetItem;
            targetItem.RequestChangeOwnership(true, bothHandsTransform);
            OnTwoHandedToggled?.Invoke(true);
        }
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
            if (targetSlot == -1) { RequestDropCurrentItem(); targetSlot = currentSlotIndex; }
            slots[targetSlot] = targetItem;
            targetItem.RequestChangeOwnership(true, leftHandTransform);
            if (targetSlot != currentSlotIndex) targetItem.gameObject.SetActive(false);
        }
        OnInventoryUpdated?.Invoke();
    }

    public void RequestDropCurrentItem()
    {
        ItemBase itemToDrop = (twoHandedItem != null) ? twoHandedItem : slots[currentSlotIndex];
        if (itemToDrop == null) return;

        if (twoHandedItem != null) { twoHandedItem = null; OnTwoHandedToggled?.Invoke(false); }
        else slots[currentSlotIndex] = null;

        itemToDrop.RequestChangeOwnership(false, null);
        itemToDrop.transform.position = Camera.main.transform.position + Camera.main.transform.forward;
        if (itemToDrop.TryGetComponent(out Rigidbody rb))
        {
            rb.AddForce((Camera.main.transform.forward + Vector3.up * 0.2f) * throwForce, ForceMode.Impulse);
            itemToDrop.BeginThrownState();
        }
        if (twoHandedItem == null && slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
        OnInventoryUpdated?.Invoke();
    }

    private void HandleSlotChange()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;
        int prev = currentSlotIndex;
        if (scroll < 0f && currentSlotIndex < slots.Length - 1) currentSlotIndex++;
        else if (scroll > 0f && currentSlotIndex > 0) currentSlotIndex--;
        if (prev != currentSlotIndex)
        {
            if (twoHandedItem == null && slots[prev] != null) slots[prev].gameObject.SetActive(false);
            if (twoHandedItem == null && slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    private void LoadInventoryData()
    {
        if (GameSessionManager.Instance == null || GameSessionManager.Instance.playerItems.Count == 0) return;

        Debug.Log($"<color=orange><b>[Inventory]</b> {GameSessionManager.Instance.playerItems.Count}개의 인벤토리 아이템 복구를 시작합니다.</color>");

        foreach (var data in GameSessionManager.Instance.playerItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(data.itemID);
            if (prefab == null)
            {
                Debug.LogError($"🚨 프리팹 DB에서 ID {data.itemID}를 찾을 수 없습니다!");
                continue;
            }

            // 1. 아이템 실체 생성
            ItemBase spawned = Instantiate(prefab);

            // 2. 내구도 데이터 복구 (내구도형 아이템일 경우)
            if (spawned is Item_Durability dur)
            {
                dur.currentDurability = data.stateValues[0];
            }

            // 3. 저장된 슬롯 위치에 정확히 배치
            slots[data.slotIndex] = spawned;

            // 4. 소유권 설정 및 물리 엔진 정지 (손 위치에 붙이기)
            spawned.RequestChangeOwnership(true, leftHandTransform);

            // 5. 현재 선택된 슬롯이 아니라면 일단 비활성화 (모델링만 숨김)
            if (data.slotIndex != currentSlotIndex)
            {
                spawned.gameObject.SetActive(false);
            }
        }

        // 💡 [중요] 모든 아이템 복구 후 UI에 "그려라!"라고 신호를 보냄
        OnInventoryUpdated?.Invoke();
        OnSlotChanged?.Invoke(currentSlotIndex);

        Debug.Log("<color=orange><b>[Inventory]</b> 인벤토리 UI 및 아이템 복구 완료.</color>");
    }
}