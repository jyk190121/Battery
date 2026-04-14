using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    public static PlayerInventory LocalInstance { get; private set; }
    public static bool IsHoldingTwoHanded => LocalInstance?.twoHandedItem != null;

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

    public float throwForce = 1f;

    public Action<int> OnSlotChanged;
    public Action OnInventoryUpdated;
    public Action<bool> OnTwoHandedToggled;

    private ItemBase lastLookedItem;
    private DepartureButton lastLookedButton;
    private MissionStartButton lastLookedMissionButton;

    // 💡 [추가됨] 문과 환원 지점 인식용 변수
    private DoorController lastLookedDoor;
    private QuestReturnPoint lastLookedReturnPoint;

    public override void OnNetworkSpawn()
    {
        leftHandTransform = FindChildByName(transform, leftHandName);
        bothHandsTransform = FindChildByName(transform, bothHandsName);

        if (IsOwner) LocalInstance = this;

        if (IsServer)
        {
            StartCoroutine(WaitOneFrameAndRestore());
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
        else if (IsOwner)
        {
            RequestSyncLateJoinerServerRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        RestoreItemsFromServer();
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

        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                if (lastLookedButton != null) lastLookedButton.Interact(this);
                else if (lastLookedMissionButton != null) lastLookedMissionButton.Interact(this);
                else if (lastLookedReturnPoint != null) lastLookedReturnPoint.Interact(this); // 💡 [추가됨] 환원 지점 상호작용
                else if (lastLookedDoor != null) // 💡 [추가됨] 문(열쇠) 상호작용
                {
                    string myKeyID = "";
                    ItemBase heldItem = twoHandedItem != null ? twoHandedItem : slots[currentSlotIndex];

                    if (heldItem != null && heldItem.itemData != null && !string.IsNullOrEmpty(heldItem.itemData.keyID))
                    {
                        myKeyID = heldItem.itemData.keyID;
                    }
                    lastLookedDoor.TryOpen(myKeyID);
                }
                else if (lastLookedItem != null) TryPickUpAction();
            }
            if (Keyboard.current[Key.G].wasPressedThisFrame) RequestDropCurrentItem();
        }

        // 폰 켜져있으면 마우스 차단 로직 (필요 시 주석 해제)
        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isPhoneActive) return;

        HandleSlotChange();
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

            if (hit.collider.TryGetComponent(out DepartureButton dBtn))
            {
                if (lastLookedButton != dBtn) { ClearHighlight(); lastLookedButton = dBtn; }
                return;
            }

            if (hit.collider.TryGetComponent(out MissionStartButton mBtn))
            {
                if (lastLookedMissionButton != mBtn) { ClearHighlight(); lastLookedMissionButton = mBtn; }
                return;
            }

            // 💡 [추가됨] 환원 지점(투명 큐브) 레이캐스트 인식
            if (hit.collider.TryGetComponent(out QuestReturnPoint returnPoint))
            {
                if (lastLookedReturnPoint != returnPoint)
                {
                    ClearHighlight();
                    lastLookedReturnPoint = returnPoint;
                    Outline outline = lastLookedReturnPoint.GetComponentInChildren<Outline>();
                    if (outline != null) outline.enabled = true;
                }
                return;
            }

            // 💡 [추가됨] 문(Door) 레이캐스트 인식
            if (hit.collider.TryGetComponent(out DoorController door))
            {
                if (lastLookedDoor != door)
                {
                    ClearHighlight();
                    lastLookedDoor = door;
                    Outline outline = lastLookedDoor.GetComponentInChildren<Outline>();
                    if (outline != null) outline.enabled = true;
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
        if (lastLookedReturnPoint != null)
        {
            Outline outline = lastLookedReturnPoint.GetComponentInChildren<Outline>();
            if (outline != null) outline.enabled = false;
            lastLookedReturnPoint = null;
        }
        if (lastLookedDoor != null)
        {
            Outline outline = lastLookedDoor.GetComponentInChildren<Outline>();
            if (outline != null) outline.enabled = false;
            lastLookedDoor = null;
        }
        lastLookedButton = null;
        lastLookedMissionButton = null;
    }

    // ... (이하 TryPickUpAction 부터 끝까지의 코드는 기존과 100% 동일하므로 생략 없이 그대로 유지하시면 됩니다) ...
    // ... (HasItem, RemoveItemByServer 함수도 그대로 두시면 됩니다) ...

    private void TryPickUpAction()
    {
        if (lastLookedItem != null && twoHandedItem == null && !lastLookedItem.isEquipped)
        {
            bool hasEmptySlot = false;
            foreach (var slot in slots) if (slot == null) hasEmptySlot = true;

            if (!hasEmptySlot) return;

            Outline outline = lastLookedItem.GetComponentInChildren<Outline>();
            if (outline != null) outline.enabled = false;

            RequestPickUpServerRpc(lastLookedItem.NetworkObjectId);
            lastLookedItem = null;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestPickUpServerRpc(ulong itemNetId, RpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
        ItemBase item = netObj.GetComponent<ItemBase>();

        if (item == null || item.isEquipped) return;

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
            if (slots[currentSlotIndex] != null && slots[currentSlotIndex] != item)
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
            Transform camTransform = Camera.main.transform;
            Vector3 startPos = camTransform.position;
            Vector3 throwDir = camTransform.forward;
            Vector3 dropPos = startPos + throwDir * 1.5f;

            if (Physics.Raycast(startPos, throwDir, out RaycastHit hit, 1.5f))
            {
                if (hit.collider.gameObject != this.gameObject)
                    dropPos = hit.point - throwDir * 0.2f;
            }

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
            if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == item) slots[i] = null;
        }

        item.gameObject.SetActive(true);
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

        int newIndex = currentSlotIndex;
        if (scroll < 0f && newIndex < slots.Length - 1) newIndex++;
        else if (scroll > 0f && newIndex > 0) newIndex--;

        if (newIndex != currentSlotIndex) RequestChangeSlotServerRpc(newIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestChangeSlotServerRpc(int newIndex) { SyncSlotChangeClientRpc(newIndex); }

    [Rpc(SendTo.Everyone)]
    private void SyncSlotChangeClientRpc(int newIndex)
    {
        if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(false);
        currentSlotIndex = newIndex;
        if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
        if (IsOwner) OnSlotChanged?.Invoke(currentSlotIndex);
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
                if (spawned is Item_Durability dur) dur.currentDurability = data.stateValue1;

                spawned.NetworkObject.SpawnWithOwnership(myId);
                SyncRestoredItemClientRpc(new NetworkObjectReference(spawned.NetworkObject), data.slotIndex);
            }
        }
        GameSessionManager.Instance.playerItems.Remove(myId);
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
                item.gameObject.SetActive(false);
            else if (twoHandedItem != null && item != twoHandedItem)
                item.gameObject.SetActive(false);

            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestSyncLateJoinerServerRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PlayerInventory[] allPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            for (int i = 0; i < p.slots.Length; i++)
            {
                if (p.slots[i] != null && p.slots[i].NetworkObject != null && p.slots[i].NetworkObject.IsSpawned)
                {
                    p.SyncRestoredItemClientRpc(new NetworkObjectReference(p.slots[i].NetworkObject), i, RpcTarget.Single(senderId, RpcTargetUse.Temp));
                }
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SyncRestoredItemClientRpc(NetworkObjectReference itemRef, int slotIdx, RpcParams rpcParams)
    {
        SyncRestoredItemClientRpc(itemRef, slotIdx);
    }

    public bool HasItem(int itemID)
    {
        if (twoHandedItem != null && twoHandedItem.itemData.itemID == itemID) return true;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].itemData.itemID == itemID) return true;
        }
        return false;
    }

    public void RemoveItemByServer(int itemID)
    {
        if (!IsServer) return;

        if (twoHandedItem != null && twoHandedItem.itemData.itemID == itemID)
        {
            twoHandedItem.NetworkObject.Despawn();
            twoHandedItem = null;
            SyncSlotChangeClientRpc(currentSlotIndex);
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].itemData.itemID == itemID)
            {
                slots[i].NetworkObject.Despawn();
                slots[i] = null;
                SyncSlotChangeClientRpc(currentSlotIndex);
                return;
            }
        }
    }
}