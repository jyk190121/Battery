using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    public static PlayerInventory LocalInstance { get; private set; }
    public ItemBase HeldItem => twoHandedItem ?? slots[currentSlotIndex];
    public static bool IsHoldingTwoHanded => LocalInstance?.twoHandedItem != null;

    [Header("Inventory Slots")]
    public ItemBase[] slots = new ItemBase[4];
    public int currentSlotIndex = 0;
    [HideInInspector] public ItemBase twoHandedItem = null;
    [HideInInspector] public bool isControlLocked = false;

    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask itemLayer;
    public LayerMask obstacleLayer; //방해물 체크


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

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        RestoreItemsFromServer();
    }

    private IEnumerator WaitOneFrameAndRestore()
    {
        while (GameSessionManager.Instance == null) yield return null;
        RestoreItemsFromServer();
    }

    private Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == targetName) return child;
        return null;
    }

    // 콜라이더를 끄는 대신 Trigger와 레이어를 제어합니다.
    private void SetItemPhysicsAndLayer(ItemBase item, bool equipped)
    {
        if (item == null) return;

        // 자식까지 포함하여 콜라이더를 찾아 Trigger 상태를 동기화합니다.
        Collider col = item.GetComponentInChildren<Collider>();
        if (col != null)
        {
            col.isTrigger = equipped;
        }

        // 물리 연산 중단/재개
        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = equipped;
        }

        // 레이어 변경 (EquippedItem <-> Item)
        item.gameObject.layer = LayerMask.NameToLayer(equipped ? "EquippedItem" : "Item");
    }

    void Update()
    {
        if (!IsOwner) return;

        // [1차 방어] 최상위 제어 잠금 (스마트폰 오픈, 기절 등 시스템 락)
        if (isControlLocked) return;
        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isPhoneActive) return;

        // 상호작용 대상 탐색 및 마우스 휠 슬롯 변경은 항상 체크합니다.
        CheckInteraction();
        HandleSlotChange();

        // [2차 & 3차 방어] 좌클릭 (아이템 사용 / 무기 공격)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 현재 들고 있는 아이템이 '무기'가 아닐 때만 인벤토리의 소모품(섬광탄 등)을 사용합니다.
            if (!IsHoldingWeapon())
            {
                // 내부 4차 방어(빈손 체크)는 HandleItemUse() 안에서 수행됨
                HandleItemUse();
            }
        }

        // 키보드 입력 (상호작용 / 버리기) - 무기를 들어도 정상 작동!
        if (Keyboard.current != null)
        {
            // 상호작용 (E)
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                if (lastLookedButton != null) lastLookedButton.Interact(this);
                else if (lastLookedReturnPoint != null) lastLookedReturnPoint.Interact(this);
                else if (lastLookedDoor != null)
                {
                    string myKeyID = "";
                    ItemBase heldItem = HeldItem;
                    if (heldItem != null && heldItem.itemData != null && !string.IsNullOrEmpty(heldItem.itemData.keyID))
                        myKeyID = heldItem.itemData.keyID;
                    lastLookedDoor.TryOpen(myKeyID);
                }
                else if (lastLookedItem != null) TryPickUpAction();
            }

            // 버리기 (G)
            if (Keyboard.current[Key.G].wasPressedThisFrame)
            {
                RequestDropCurrentItem();
            }
        }
    }
    //무기를 손에 들고 있는 경우.
    private bool IsHoldingWeapon()
    {
       
        ItemBase heldItem = HeldItem;

        if (heldItem == null || heldItem.itemData == null) return false;

        // 섬광탄의 category는 'Consumable'이므로 아래 조건은 즉시 false를 반환합니다.
        return heldItem.itemData.category == ItemCategory.Weapon;
    }

    private void HandleItemUse()
    {
        // 현재 손에 든 아이템 확인
        ItemBase heldItem = twoHandedItem != null ? twoHandedItem : slots[currentSlotIndex];

        if (heldItem != null)
        {
            // 카메라가 바라보는 방향을 매개변수로 전달합니다.
            Vector3 lookDir = Camera.main.transform.forward;
            heldItem.RequestUseItem(lookDir);

            if (heldItem is Item_Flashbang)
            {
                ClearItemReference(heldItem);
            }

        }

    }

    private void ConsumeItemFromInventory(ItemBase item)
    {
        // 소유자 클라이언트의 인벤토리 배열에서만 비워줌 
        // (실제 오브젝트는 섬광탄 스크립트의 Despawn에 의해 파괴됨)
        if (item == twoHandedItem)
        {
            twoHandedItem = null;
            OnTwoHandedToggled?.Invoke(false);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == item)
            {
                slots[i] = null;
                break;
            }
        }

        OnInventoryUpdated?.Invoke();
        // UI 업데이트를 위해 슬롯 변경 이벤트 한 번 더 호출 가능
        OnSlotChanged?.Invoke(currentSlotIndex);
    }
    private void CheckInteraction()
    {
        if (Camera.main == null) return;
        Vector3 camPos = Camera.main.transform.position;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, itemLayer))
        {
            Vector3 checkEndPos = hit.point - (ray.direction * 0.1f);
            if (Physics.Linecast(camPos, checkEndPos, obstacleLayer))
            {
                ClearHighlight();
                return; // 벽이나 닫힌 문에 가려졌음! 
            }


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

            if (hit.collider.TryGetComponent(out QuestReturnPoint returnPoint))
            {
                // 내 퀘스트 목록에 있는 포인트일 때만 반응 
                if (returnPoint.IsInteractable())
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
                // 💡 내 퀘스트가 아니라면 아무것도 잡히지 않은 것처럼 처리
                else
                {
                    ClearHighlight();
                    return;
                }
            }

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
    }

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

        if (item is QuestCollectionItem questItem)
        {
            questItem.lastHolderId = rpcParams.Receive.SenderClientId;
        }
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

        // 💡 콜라이더를 끄는 대신 Trigger 상태로 전환
        SetItemPhysicsAndLayer(item, true);

        if (item.itemData.handType == HandType.TwoHand)
        {
            slots[emptySlotIndex] = item;
            twoHandedItem = item;

            // 두 손 아이템 장착 시 기존 한 손 아이템만 시각적으로 비활성화 (물리는 Trigger 유지)
            if (slots[currentSlotIndex] != null && slots[currentSlotIndex] != item)
                slots[currentSlotIndex].gameObject.SetActive(false);

            item.ExecuteChangeOwnership(true, bothHandsTransform);
            if (IsOwner) OnTwoHandedToggled?.Invoke(true);
        }
        else
        {
            slots[emptySlotIndex] = item;
            item.ExecuteChangeOwnership(true, leftHandTransform);

            // 현재 슬롯이 아니면 시각적으로만 비활성화
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
            Vector3 throwDir = camTransform.forward;
            Vector3 dropPos = camTransform.position + throwDir * 1.5f;

            if (Physics.Raycast(camTransform.position, throwDir, out RaycastHit hit, 1.5f))
            {
                if (hit.collider.gameObject != this.gameObject)
                    dropPos = hit.point - throwDir * 0.2f;
            }

            RequestDropServerRpc(itemToDrop.NetworkObjectId, dropPos, throwDir);
        }
    }
    /// <summary>
    /// [서버 전용] 외부 요인(몬스터 피격 등)으로 인해 현재 손에 든 아이템을 강제로 떨어뜨립니다.
    /// </summary>
    public void ForceDropCurrentItemServer()
    {
        if (!IsServer) return;

        // HeldItem 프로퍼티를 사용하여 양손 무기든 단축키 아이템이든 현재 손에 든 것을 가져옴
        ItemBase itemToDrop = HeldItem;

        if (itemToDrop != null)
        {
            // 플레이어 몸통 살짝 위에서 바닥으로 툭 떨어지도록 위치/방향 설정
            Vector3 dropOrigin = transform.position + Vector3.up * 0.8f;
            Vector3 dropDir = (transform.forward * 0.5f + Vector3.up * 0.5f).normalized;

            ForceDropItem(itemToDrop, dropOrigin, dropDir);
            Debug.Log($"<color=orange>[Inventory]</color> 충격으로 인해 {itemToDrop.itemData.itemName}을(를) 떨어뜨렸습니다!");
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
            if (slots[i] == item) slots[i] = null;

        // 💡 버릴 때 다시 일반 콜라이더 및 레이어로 복구
        SetItemPhysicsAndLayer(item, false);

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
        // 💡 여기서 SetActive(false/true)를 수행하지만, 콜라이더를 명시적으로 끄는 코드는 삭제되었습니다.
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

            // 복구 시에도 물리 상태 설정
            SetItemPhysicsAndLayer(item, true);

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
            if (slots[i] != null && slots[i].itemData.itemID == itemID) return true;
        return false;
    }

    public bool RemoveItemByServer(int itemID)
    {
        if (!IsServer) return false;

        ItemBase itemToRemove = HeldItem;

        if (itemToRemove != null && itemToRemove.itemData.itemID == itemID)
        {
            itemToRemove.NetworkObject.Despawn();
            int slotIdx = (twoHandedItem == itemToRemove) ? -1 : currentSlotIndex;
            bool isTwoHand = (twoHandedItem == itemToRemove);

            NotifySyncItemRemovedClientRpc(slotIdx, isTwoHand);

            return true; //  성공적으로 삭제했음을 알림!
        }
        else
        {
            return false; //  삭제 실패 (손에 없거나 ID가 다름)
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestRemoveItemServerRpc(int itemID)
    {
        // 클라이언트의 요청을 받아 서버가 대신 마스터 함수를 실행해줍니다.
        RemoveItemByServer(itemID);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifySyncItemRemovedClientRpc(int slotIdx, bool isTwoHand)
    {
        if (isTwoHand)
        {
            twoHandedItem = null;
            if (IsOwner) OnTwoHandedToggled?.Invoke(false);
        }
        else if (slotIdx != -1)
        {
            slots[slotIdx] = null; // 정확히 그 슬롯만 비움
        }

        // UI 즉시 갱신 (4번 아이템 썼을 때 뒤늦게 사라지는 현상 해결)
        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    public void DropAllItemsOnDeathServer()
    {
        if (!IsServer) return;

        // 플레이어 몸통 살짝 위를 드롭 기준점으로 설정
        Vector3 dropOrigin = transform.position + Vector3.up * 0.8f;

        // 1. 양손 아이템 떨어뜨리기
        if (twoHandedItem != null)
        {
            Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.5f, 1.5f), UnityEngine.Random.Range(-1f, 1f)).normalized;
            ForceDropItem(twoHandedItem, dropOrigin, randomDir);
        }

        // 2. 인벤토리(단축키) 슬롯 아이템 모두 떨어뜨리기
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.5f, 1.5f), UnityEngine.Random.Range(-1f, 1f)).normalized;
                ForceDropItem(slots[i], dropOrigin, randomDir);
            }
        }
    }

    private void ForceDropItem(ItemBase item, Vector3 pos, Vector3 dir)
    {
        if (item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned)
        {
            // 소유권 박탈 후, 기존에 만들어둔 '버리기 RPC'를 그대로 호출하여 모든 클라이언트 동기화
            item.NetworkObject.RemoveOwnership();
            NotifyItemDroppedClientRpc(item.NetworkObjectId, pos, dir);
        }
    }

    public void ConsumeKeyItem(ItemBase item)
    {
        if (item == null) return;

        if (IsServer)
        {
            RemoveItemByServer(item.itemData.itemID);
        }
        else
        {
            RequestRemoveItemServerRpc(item.itemData.itemID);
        }
    }



    public void SetControlLock(bool locked)
    {
        isControlLocked = locked;
        if (locked) ClearHighlight();
    }
    public void ClearItemReference(ItemBase item)
    {
        if (item == twoHandedItem)
        {
            twoHandedItem = null;
            if (IsOwner) OnTwoHandedToggled?.Invoke(false);
        }
        else
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    slots[i] = null;
                    break;
                }
            }
        }

        // 내 화면(UI) 즉시 새로고침
        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }
}