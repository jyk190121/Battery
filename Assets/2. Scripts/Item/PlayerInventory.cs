using System;
using System.Collections;
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

    [Header("Hand Transform Names")]
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
            Debug.LogError($"[PlayerInventory] 손 Transform을 찾지 못했습니다!");

        if (IsOwner) LocalInstance = this;

        if (IsServer) StartCoroutine(WaitOneFrameAndRestore());
    }

    private IEnumerator WaitOneFrameAndRestore()
    {
        yield return null;
        RestoreItemsFromServer();
    }

    private Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == targetName) return child;
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
                if (lastLookedButton != null) lastLookedButton.Interact(this);
                else if (lastLookedItem != null) TryPickUpAction();
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
            ItemBase targetItem = hit.collider.GetComponentInParent<ItemBase>();
            if (targetItem != null && !targetItem.isEquipped)
            {
                if (lastLookedItem != targetItem)
                {
                    ClearHighlight();
                    lastLookedItem = targetItem;
                    Outline outline = lastLookedItem.GetComponentInChildren<Outline>();
                    if (outline != null) outline.enabled = true;
                }
                return;
            }

            if (hit.collider.TryGetComponent(out DepartureButton targetButton))
            {
                if (lastLookedButton != targetButton)
                {
                    ClearHighlight();
                    lastLookedButton = targetButton;
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
            Outline outline = lastLookedItem.GetComponentInChildren<Outline>();
            if (outline != null) outline.enabled = false;
            lastLookedItem = null;
        }
        lastLookedButton = null;
    }

    private void TryPickUpAction()
    {
        if (lastLookedItem != null && twoHandedItem == null && !lastLookedItem.isEquipped)
        {
            Outline outline = lastLookedItem.GetComponentInChildren<Outline>();
            if (outline != null) outline.enabled = false;

            RequestPickUpServerRpc(lastLookedItem.NetworkObjectId);
            lastLookedItem = null;
        }
    }

    // [경고 해결] ServerRpc -> Rpc 최신 문법 교체. ServerRpcParams -> RpcParams
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestPickUpServerRpc(ulong itemNetId, RpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
        ItemBase item = netObj.GetComponent<ItemBase>();

        // [이중 줍기 완벽 방어] 서버 측에서 이미 주워진 아이템인지 즉시 검증
        if (item == null || item.isEquipped) return;

        // 다른 누군가의 후속 요청을 막기 위해 상태 즉시 잠금
        item.isEquipped = true;

        item.NetworkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
        NotifyPickUpClientRpc(itemNetId);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyPickUpClientRpc(ulong itemNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
        ItemBase item = netObj.GetComponent<ItemBase>();

        int emptySlotIndex = -1;
        if (slots[currentSlotIndex] == null) emptySlotIndex = currentSlotIndex;
        else
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) { emptySlotIndex = i; break; }
        }

        if (emptySlotIndex == -1) return;

        if (item.itemData.handType == HandType.TwoHand)
        {
            slots[emptySlotIndex] = item;
            twoHandedItem = item;

            if (IsOwner && slots[currentSlotIndex] != null && slots[currentSlotIndex] != item)
                slots[currentSlotIndex].gameObject.SetActive(false);

            item.ExecuteChangeOwnership(true, bothHandsTransform);
            if (IsOwner) OnTwoHandedToggled?.Invoke(true);
        }
        else
        {
            slots[emptySlotIndex] = item;
            item.ExecuteChangeOwnership(true, leftHandTransform);
            if (emptySlotIndex != currentSlotIndex) item.gameObject.SetActive(false);
        }

        if (IsOwner) OnInventoryUpdated?.Invoke();
    }

    public void RequestDropCurrentItem()
    {
        ItemBase itemToDrop = null;

        if (twoHandedItem != null) itemToDrop = twoHandedItem;
        else if (slots[currentSlotIndex] != null) itemToDrop = slots[currentSlotIndex];

        if (itemToDrop != null)
        {
            Vector3 dropPos = Camera.main.transform.position + Camera.main.transform.forward;
            Vector3 throwDir = Camera.main.transform.forward;

            RequestDropServerRpc(itemToDrop.NetworkObjectId, dropPos, throwDir);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestDropServerRpc(ulong itemNetId, Vector3 dropPos, Vector3 throwDir)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
        ItemBase item = netObj.GetComponent<ItemBase>();

        item.NetworkObject.RemoveOwnership();
        NotifyItemDroppedClientRpc(itemNetId, dropPos, throwDir);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyItemDroppedClientRpc(ulong itemNetId, Vector3 dropPos, Vector3 throwDir)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
        ItemBase item = netObj.GetComponent<ItemBase>();

        if (item == twoHandedItem)
        {
            twoHandedItem = null;
            if (IsOwner) OnTwoHandedToggled?.Invoke(false);
            if (IsOwner && slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == item) slots[i] = null;
        }

        item.transform.position = dropPos;
        item.ExecuteChangeOwnership(false, null);
        if (IsServer)
        {
            if (item.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce((throwDir + Vector3.up * 0.2f) * throwForce, ForceMode.Impulse);
                item.BeginThrownState();
            }
        }

        if (IsOwner) OnInventoryUpdated?.Invoke();
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

    private void RestoreItemsFromServer()
    {
        ulong myId = OwnerClientId;
        if (GameSessionManager.Instance.playerItems.TryGetValue(myId, out var savedItems))
        {
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

    [Rpc(SendTo.Everyone)]
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