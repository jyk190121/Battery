using System;
using System.Collections; // 💡 코루틴을 사용하기 위해 추가
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    public static PlayerInventory LocalInstance { get; private set; }

    [Header("Inventory Slots")]
    public ItemBase[] slots = new ItemBase[4];
    public int currentSlotIndex = 0;
    [HideInInspector] public ItemBase twoHandedItem = null;

    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask itemLayer;

    [Header("Hand Transform Names (자식 오브젝트 이름)")]
    public string leftHandName = "OneHandle";
    public string bothHandsName = "BothHandle";

    [HideInInspector] public Transform leftHandTransform;
    [HideInInspector] public Transform bothHandsTransform;

    public float throwForce = 7f;

    public Action<int> OnSlotChanged;
    public Action OnInventoryUpdated;
    public Action<bool> OnTwoHandedToggled;

    private ItemBase lastLookedItem;
    private DepartureButton lastLookedButton;

    public override void OnNetworkSpawn()
    {
        leftHandTransform = FindChildByName(transform, leftHandName);
        bothHandsTransform = FindChildByName(transform, bothHandsName);

        if (leftHandTransform == null || bothHandsTransform == null)
        {
            Debug.LogError($"[PlayerInventory] 손 Transform을 찾지 못했습니다! 자식 오브젝트 이름을 확인해주세요.");
        }

        if (IsOwner) LocalInstance = this;

        if (IsServer)
        {
            // 💡 [에러 수정] 씬이 열리자마자 바로 부모를 바꾸면 에러가 나므로 코루틴으로 지연 실행!
            StartCoroutine(DelayedRestoreItems());
        }
    }

    // 💡 0.2초 대기 후 복구를 시작하는 코루틴
    private IEnumerator DelayedRestoreItems()
    {
        yield return new WaitForSeconds(0.2f);
        RestoreItemsFromServer();
    }

    private Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName) return child;
        }
        return null;
    }

    void Update()
    {
        if (!IsOwner) return;

        CheckInteraction();
        HandleSlotChange();

        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                if (lastLookedButton != null)
                {
                    lastLookedButton.Interact(this);
                }
                else if (lastLookedItem != null)
                {
                    TryPickUpAction();
                }
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

    private void TryPickUpAction()
    {
        if (lastLookedItem != null) LocalPickUpLogic(lastLookedItem);
    }

    private void LocalPickUpLogic(ItemBase targetItem)
    {
        if (targetItem.TryGetComponent(out Outline outline)) outline.enabled = false;
        lastLookedItem = null;

        if (twoHandedItem != null)
        {
            Debug.Log("<color=red>양손을 사용 중이라 다른 아이템을 주울 수 없습니다!</color>");
            return;
        }

        int emptySlotIndex = -1;
        if (slots[currentSlotIndex] == null)
        {
            emptySlotIndex = currentSlotIndex;
        }
        else
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) { emptySlotIndex = i; break; }
            }
        }

        if (emptySlotIndex == -1)
        {
            Debug.Log("<color=red>인벤토리가 가득 찼습니다! (G키로 버려야 합니다)</color>");
            return;
        }

        if (targetItem.itemData.handType == HandType.TwoHand)
        {
            slots[emptySlotIndex] = targetItem;
            twoHandedItem = targetItem;

            if (slots[currentSlotIndex] != null && slots[currentSlotIndex] != targetItem)
            {
                slots[currentSlotIndex].gameObject.SetActive(false);
            }

            targetItem.RequestChangeOwnership(true, bothHandsTransform);
            OnTwoHandedToggled?.Invoke(true);
        }
        else
        {
            slots[emptySlotIndex] = targetItem;
            targetItem.RequestChangeOwnership(true, leftHandTransform);

            if (emptySlotIndex != currentSlotIndex)
            {
                targetItem.gameObject.SetActive(false);
            }
        }

        OnInventoryUpdated?.Invoke();
    }

    public void RequestDropCurrentItem()
    {
        ItemBase itemToDrop = null;

        if (twoHandedItem != null)
        {
            itemToDrop = twoHandedItem;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == twoHandedItem)
                {
                    slots[i] = null;
                    break;
                }
            }

            twoHandedItem = null;
            OnTwoHandedToggled?.Invoke(false);

            if (slots[currentSlotIndex] != null)
            {
                slots[currentSlotIndex].gameObject.SetActive(true);
            }
        }
        else if (slots[currentSlotIndex] != null)
        {
            itemToDrop = slots[currentSlotIndex];
            slots[currentSlotIndex] = null;
        }

        if (itemToDrop != null)
        {
            itemToDrop.RequestChangeOwnership(false, null);
            itemToDrop.transform.position = Camera.main.transform.position + Camera.main.transform.forward;
            if (itemToDrop.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce((Camera.main.transform.forward + Vector3.up * 0.2f) * throwForce, ForceMode.Impulse);
                itemToDrop.BeginThrownState();
            }
            OnInventoryUpdated?.Invoke();
        }
    }

    private void HandleSlotChange()
    {
        if (twoHandedItem != null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;

        int prev = currentSlotIndex;

        if (scroll < 0f && currentSlotIndex < slots.Length - 1) currentSlotIndex++;
        else if (scroll > 0f && currentSlotIndex > 0) currentSlotIndex--;

        if (prev != currentSlotIndex)
        {
            if (slots[prev] != null) slots[prev].gameObject.SetActive(false);
            if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    // ==========================================================
    // 멀티플레이 아이템 복구 시스템
    // ==========================================================
    private void RestoreItemsFromServer()
    {
        ulong myId = OwnerClientId;
        if (GameSessionManager.Instance.playerItems.TryGetValue(myId, out var savedItems))
        {
            Debug.Log($"<color=orange><b>[Inventory]</b> {savedItems.Count}개의 인벤토리 아이템 복구를 시작합니다.</color>");
            foreach (var data in savedItems)
            {
                ItemBase prefab = GameSessionManager.Instance.GetPrefab(data.itemID);
                if (prefab == null) continue;

                ItemBase spawned = Instantiate(prefab);
                if (spawned is Item_Durability dur) dur.currentDurability = data.stateValues[0];

                spawned.NetworkObject.SpawnWithOwnership(myId);
                SyncRestoredItemClientRpc(new NetworkObjectReference(spawned.NetworkObject), data.slotIndex);
            }
        }
    }

    [ClientRpc]
    private void SyncRestoredItemClientRpc(NetworkObjectReference itemRef, int slotIdx)
    {
        if (itemRef.TryGet(out NetworkObject netObj))
        {
            ItemBase item = netObj.GetComponent<ItemBase>();
            slots[slotIdx] = item;

            Transform targetHand = (item.itemData.handType == HandType.TwoHand) ? bothHandsTransform : leftHandTransform;
            item.ExecuteChangeOwnership(true, targetHand);

            if (item.itemData.handType == HandType.TwoHand)
            {
                twoHandedItem = item;
                OnTwoHandedToggled?.Invoke(true);
            }

            if (slotIdx != currentSlotIndex && item.itemData.handType != HandType.TwoHand)
            {
                item.gameObject.SetActive(false);
            }
            else if (twoHandedItem != null && item != twoHandedItem)
            {
                item.gameObject.SetActive(false);
            }

            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }
}