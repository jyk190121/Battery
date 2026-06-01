using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// 플레이어의 인벤토리 시스템, 상호작용(Raycast), 아이템 관리 및 네트워크 동기화를 담당.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    public static PlayerInventory LocalInstance { get; private set; }
    public static bool IsHoldingTwoHanded => LocalInstance?.twoHandedItem != null;
    public ItemBase HeldItem => (twoHandedItem != null) ? twoHandedItem : slots[currentSlotIndex];

    [Header("Inventory Slots")]
    public ItemBase[] slots = new ItemBase[4];
    public int currentSlotIndex = 0;

    [HideInInspector] public ItemBase twoHandedItem = null;
    [HideInInspector] public bool isControlLocked = false;

    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask itemLayer;
    public LayerMask obstacleLayer;

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

    private Coroutine curseRoutine = null;
    private bool hasCurseAggro = false;
    private bool hasCurseHallucination = false;


    // ==========
    // 1.초기화
    // ==========

    public override void OnNetworkSpawn()
    {
        leftHandTransform = FindChildByName(transform, leftHandName);
        bothHandsTransform = FindChildByName(transform, bothHandsName);

        if (IsOwner)
        {
            LocalInstance = this;
        }

        if (IsServer)
        {
            StartCoroutine(WaitOneFrameAndRestoreRoutine());
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

    private IEnumerator WaitOneFrameAndRestoreRoutine()
    {
        while (GameSessionManager.Instance == null)
        {
            yield return null;
        }
        RestoreItemsFromServer();
    }

    private void Update()
    {
        if (!IsOwner) { return; }
        if (isControlLocked) { return; }
        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isPhoneActive) { return; }

        CheckInteraction();
        HandleInputs();
        HandleSlotChange();
    }


    // ==========================================
    // 2. 사용자 입력 및 상호작용 (Input & Interaction)
    // ==========================================

    private void CheckInteraction()
    {
        if (Camera.main == null) { return; }

        Vector3 cameraPosition = Camera.main.transform.position;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, itemLayer))
        {
            Vector3 checkEndPosition = hit.point - (ray.direction * 0.1f);
            if (Physics.Linecast(cameraPosition, checkEndPosition, obstacleLayer))
            {
                ClearHighlight();
                return;
            }

            if (hit.collider.TryGetComponent(out ItemBase targetItem))
            {
                targetItem = hit.collider.GetComponentInParent<ItemBase>();
            }

            if (targetItem != null && !targetItem.isEquipped)
            {
                if (lastLookedItem != targetItem)
                {
                    ClearHighlight();
                    lastLookedItem = targetItem;
                    EnableOutline(lastLookedItem);
                }
                return;
            }

            if (hit.collider.TryGetComponent(out DepartureButton departureBtn))
            {
                if (lastLookedButton != departureBtn)
                {
                    ClearHighlight();
                    lastLookedButton = departureBtn;
                }
                return;
            }

            if (hit.collider.TryGetComponent(out QuestReturnPoint returnPoint))
            {
                if (returnPoint.IsInteractable())
                {
                    if (lastLookedReturnPoint != returnPoint)
                    {
                        ClearHighlight();
                        lastLookedReturnPoint = returnPoint;
                        EnableOutline(lastLookedReturnPoint);
                    }
                    return;
                }
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
                    EnableOutline(lastLookedDoor);
                }
                return;
            }
        }
        ClearHighlight();
    }

    private void HandleInputs()
    {
        if (Keyboard.current == null) { return; }

        if (Keyboard.current[Key.E].wasPressedThisFrame)
        {
            if (lastLookedButton != null)
            {
                lastLookedButton.Interact(this);
            }
            else if (lastLookedReturnPoint != null)
            {
                lastLookedReturnPoint.Interact(this);
            }
            else if (lastLookedDoor != null)
            {
                lastLookedDoor.TryOpen();
            }
            else if (lastLookedItem != null)
            {
                TryPickUpAction();
            }
        }

        if (Keyboard.current[Key.G].wasPressedThisFrame)
        {
            RequestDropCurrentItem();
        }
    }

    private void EnableOutline(Component targetComponent)
    {
        Outline outline = targetComponent.GetComponentInChildren<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
        }
    }

    public void ClearHighlight()
    {
        if (lastLookedItem != null)
        {
            Outline outline = lastLookedItem.GetComponentInChildren<Outline>();
            if (outline != null) { outline.enabled = false; }
            lastLookedItem = null;
        }

        if (lastLookedReturnPoint != null)
        {
            Outline outline = lastLookedReturnPoint.GetComponentInChildren<Outline>();
            if (outline != null) { outline.enabled = false; }
            lastLookedReturnPoint = null;
        }

        if (lastLookedDoor != null)
        {
            Outline outline = lastLookedDoor.GetComponentInChildren<Outline>();
            if (outline != null) { outline.enabled = false; }
            lastLookedDoor = null;
        }

        lastLookedButton = null;
    }

    public void SetControlLock(bool isLocked)
    {
        isControlLocked = isLocked;
        if (isLocked)
        {
            ClearHighlight();
        }
    }


    // ==========================================
    // 3. 아이템 습득 (Pick Up)
    // ==========================================

    private void TryPickUpAction()
    {
        if (lastLookedItem != null && twoHandedItem == null && !lastLookedItem.isEquipped)
        {
            bool hasEmptySlot = false;
            foreach (ItemBase slotItem in slots)
            {
                if (slotItem == null)
                {
                    hasEmptySlot = true;
                    break;
                }
            }

            if (!hasEmptySlot) { return; }

            Outline outline = lastLookedItem.GetComponentInChildren<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            RequestPickUpServerRpc(lastLookedItem.NetworkObjectId);
            lastLookedItem = null;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestPickUpServerRpc(ulong itemNetworkId, RpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject networkObj)) { return; }

        ItemBase item = networkObj.GetComponent<ItemBase>();
        if (item == null || item.isEquipped) { return; }

        item.isEquipped = true;
        item.NetworkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
        NotifyPickUpClientRpc(itemNetworkId);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyPickUpClientRpc(ulong itemNetworkId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject networkObj)) { return; }

        ItemBase item = networkObj.GetComponent<ItemBase>();
        int emptySlotIndex = -1;

        if (slots[currentSlotIndex] == null)
        {
            emptySlotIndex = currentSlotIndex;
        }
        else
        {
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (slots[slotIndex] == null)
                {
                    emptySlotIndex = slotIndex;
                    break;
                }
            }
        }

        if (emptySlotIndex == -1) { return; }

        SetItemPhysicsAndLayer(item, true);
        bool isWeapon = item.itemData.category == ItemCategory.Weapon;

        if (item.itemData.handType == HandType.TwoHand)
        {
            slots[emptySlotIndex] = item;
            twoHandedItem = item;

            if (slots[currentSlotIndex] != null && slots[currentSlotIndex] != item)
            {
                slots[currentSlotIndex].gameObject.SetActive(false);
            }

            Transform targetHand = isWeapon ? leftHandTransform : bothHandsTransform;
            item.ExecuteChangeOwnership(true, targetHand);

            if (IsOwner)
            {
                OnTwoHandedToggled?.Invoke(true);
            }
        }
        else
        {
            slots[emptySlotIndex] = item;
            item.ExecuteChangeOwnership(true, leftHandTransform);

            if (emptySlotIndex != currentSlotIndex)
            {
                item.gameObject.SetActive(false);
            }
        }

        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            RefreshQuestDebuffTiming();
        }
    }


    // ==========================================
    // 4. 아이템 투척 및 버리기 (Drop)
    // ==========================================

    public void RequestDropCurrentItem()
    {
        ItemBase itemToDrop = null;
        if (twoHandedItem != null)
        {
            itemToDrop = twoHandedItem;
        }
        else if (slots[currentSlotIndex] != null)
        {
            itemToDrop = slots[currentSlotIndex];
        }

        if (itemToDrop != null)
        {
            Transform cameraTransform = Camera.main.transform;
            Vector3 throwDirection = cameraTransform.forward;
            Vector3 dropPosition = cameraTransform.position + (throwDirection * 1.5f);

            if (Physics.Raycast(cameraTransform.position, throwDirection, out RaycastHit hit, 1.5f))
            {
                if (hit.collider.gameObject != this.gameObject)
                {
                    dropPosition = hit.point - (throwDirection * 0.2f);
                }
            }

            RequestDropServerRpc(itemToDrop.NetworkObjectId, dropPosition, throwDirection);
        }
    }

    public void ForceDropCurrentItemServer()
    {
        if (!IsServer) { return; }

        ItemBase itemToDrop = HeldItem;
        if (itemToDrop != null)
        {
            Vector3 dropOrigin = transform.position + (Vector3.up * 0.8f);
            Vector3 dropDirection = (transform.forward * 0.5f + Vector3.up * 0.5f).normalized;

            ForceDropItem(itemToDrop, dropOrigin, dropDirection);
        }
    }

    public void DropAllItemsOnDeathServer()
    {
        if (!IsServer) { return; }

        Vector3 dropOrigin = transform.position + (Vector3.up * 0.8f);

        if (twoHandedItem != null)
        {
            Vector3 randomDirection = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.5f, 1.5f), UnityEngine.Random.Range(-1f, 1f)).normalized;
            ForceDropItem(twoHandedItem, dropOrigin, randomDirection);
        }

        foreach (ItemBase slotItem in slots)
        {
            if (slotItem != null)
            {
                Vector3 randomDirection = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.5f, 1.5f), UnityEngine.Random.Range(-1f, 1f)).normalized;
                ForceDropItem(slotItem, dropOrigin, randomDirection);
            }
        }
    }

    private void ForceDropItem(ItemBase item, Vector3 position, Vector3 direction)
    {
        if (item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned)
        {
            item.NetworkObject.RemoveOwnership();
            NotifyItemDroppedClientRpc(item.NetworkObjectId, position, direction);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestDropServerRpc(ulong itemNetworkId, Vector3 dropPosition, Vector3 throwDirection)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject networkObj)) { return; }

        ItemBase item = networkObj.GetComponent<ItemBase>();
        item.NetworkObject.RemoveOwnership();
        NotifyItemDroppedClientRpc(itemNetworkId, dropPosition, throwDirection);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyItemDroppedClientRpc(ulong itemNetworkId, Vector3 dropPosition, Vector3 throwDirection)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject networkObj)) { return; }

        ItemBase item = networkObj.GetComponent<ItemBase>();

        if (item == twoHandedItem)
        {
            twoHandedItem = null;
            if (IsOwner)
            {
                OnTwoHandedToggled?.Invoke(false);
            }
            if (slots[currentSlotIndex] != null)
            {
                slots[currentSlotIndex].gameObject.SetActive(true);
            }
        }

        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            if (slots[slotIndex] == item)
            {
                slots[slotIndex] = null;
            }
        }

        SetItemPhysicsAndLayer(item, false);

        item.gameObject.SetActive(true);
        item.transform.position = dropPosition;
        item.ExecuteChangeOwnership(false, null);

        if (IsServer)
        {
            if (item.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce((throwDirection + Vector3.up * 0.2f) * throwForce, ForceMode.Impulse);
                item.BeginThrownState();
            }
        }

        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            RefreshQuestDebuffTiming();
        }
    }


    // ==========================================
    // 5. 아이템 삭제 및 내구도 파괴 로직 (Remove & Broken)
    // ==========================================

    public bool HasItem(int itemID)
    {
        if (twoHandedItem != null && twoHandedItem.itemData.itemID == itemID) { return true; }

        foreach (ItemBase slotItem in slots)
        {
            if (slotItem != null && slotItem.itemData.itemID == itemID) { return true; }
        }
        return false;
    }

    public void ClearItemReference(ItemBase item)
    {
        if (item == twoHandedItem)
        {
            twoHandedItem = null;
            if (IsOwner) { OnTwoHandedToggled?.Invoke(false); }
        }
        else
        {
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (slots[slotIndex] == item)
                {
                    slots[slotIndex] = null;
                    break;
                }
            }
        }

        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    public bool RemoveItemByServer(int itemID)
    {
        if (!IsServer) { return false; }

        ItemBase itemToRemove = null;
        int targetSlotIndex = -1;
        bool isTwoHand = false;

        if (twoHandedItem != null && twoHandedItem.itemData.itemID == itemID)
        {
            itemToRemove = twoHandedItem;
            isTwoHand = true;
        }
        else
        {
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (slots[slotIndex] != null && slots[slotIndex].itemData.itemID == itemID)
                {
                    itemToRemove = slots[slotIndex];
                    targetSlotIndex = slotIndex;
                    break;
                }
            }
        }

        if (itemToRemove != null)
        {
            NotifySyncItemRemovedClientRpc(targetSlotIndex, isTwoHand);

            if (itemToRemove.NetworkObject != null && itemToRemove.NetworkObject.IsSpawned)
            {
                itemToRemove.NetworkObject.Despawn();
            }
            return true;
        }

        return false;
    }

    public void RemoveBrokenItem(ItemBase brokenItem)
    {
        if (!IsServer || brokenItem == null) { return; }

        ulong networkId = brokenItem.NetworkObjectId;
        ClearBrokenItemLocal(networkId);
        RemoveBrokenItemClientRpc(networkId);
    }

    private void ClearBrokenItemLocal(ulong networkId)
    {
        if (twoHandedItem != null && twoHandedItem.NetworkObjectId == networkId)
        {
            twoHandedItem = null;
            OnTwoHandedToggled?.Invoke(false);
        }

        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            if (slots[slotIndex] != null && slots[slotIndex].NetworkObjectId == networkId)
            {
                slots[slotIndex] = null;
                break;
            }
        }

        OnInventoryUpdated?.Invoke();

        if (TryGetComponent(out PlayerEquipment equipment))
        {
            equipment.UpdateWeaponStatus();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestRemoveItemServerRpc(int itemID)
    {
        RemoveItemByServer(itemID);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifySyncItemRemovedClientRpc(int slotIndex, bool isTwoHand)
    {
        if (isTwoHand)
        {
            twoHandedItem = null;
            if (IsOwner) { OnTwoHandedToggled?.Invoke(false); }
        }
        else if (slotIndex != -1)
        {
            slots[slotIndex] = null;
        }

        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
            RefreshQuestDebuffTiming();
        }
    }

    [ClientRpc]
    private void RemoveBrokenItemClientRpc(ulong networkId)
    {
        if (IsServer) { return; }
        ClearBrokenItemLocal(networkId);
    }


    // ==========================================
    // 6. 슬롯 및 인벤토리 데이터 동기화 (Slot & Restoration Sync)
    // ==========================================

    private void HandleSlotChange()
    {
        if (twoHandedItem != null) { return; }

        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (scrollValue == 0f) { return; }

        int newIndex = currentSlotIndex;
        if (scrollValue < 0f && newIndex < slots.Length - 1)
        {
            newIndex++;
        }
        else if (scrollValue > 0f && newIndex > 0)
        {
            newIndex--;
        }

        if (newIndex != currentSlotIndex)
        {
            RequestChangeSlotServerRpc(newIndex);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestChangeSlotServerRpc(int newIndex)
    {
        SyncSlotChangeClientRpc(newIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestClearSlotServerRpc(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < slots.Length)
        {
            slots[slotIndex] = null;
            ClearSlotClientRpc(slotIndex);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SyncSlotChangeClientRpc(int newIndex)
    {
        if (slots[currentSlotIndex] != null)
        {
            slots[currentSlotIndex].gameObject.SetActive(false);
        }

        currentSlotIndex = newIndex;
        ItemBase newHeldItem = slots[currentSlotIndex];

        if (newHeldItem != null)
        {
            newHeldItem.gameObject.SetActive(true);
        }

        if (TryGetComponent(out PlayerEquipment equipment))
        {
            equipment.OnSlotItemChanged(newHeldItem);
        }

        if (IsOwner)
        {
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    [ClientRpc]
    private void ClearSlotClientRpc(int slotIndex)
    {
        if (!IsServer)
        {
            slots[slotIndex] = null;
        }
    }

    private void RestoreItemsFromServer()
    {
        ulong localClientId = OwnerClientId;
        if (GameSessionManager.Instance.playerItems.TryGetValue(localClientId, out var savedItems))
        {
            foreach (ItemSaveData data in savedItems)
            {
                ItemBase itemPrefab = GameSessionManager.Instance.GetPrefab(data.itemID);
                if (itemPrefab == null) { continue; }

                ItemBase spawnedItem = Instantiate(itemPrefab);
                if (spawnedItem is Item_Durability durabilityItem)
                {
                    durabilityItem.currentDurability = data.stateValue1;
                }

                spawnedItem.NetworkObject.SpawnWithOwnership(localClientId);
                SyncRestoredItemClientRpc(new NetworkObjectReference(spawnedItem.NetworkObject), data.slotIndex);
            }
        }
        GameSessionManager.Instance.playerItems.Remove(localClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestSyncLateJoinerServerRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PlayerInventory[] allPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);

        foreach (PlayerInventory player in allPlayers)
        {
            for (int slotIndex = 0; slotIndex < player.slots.Length; slotIndex++)
            {
                if (player.slots[slotIndex] != null && player.slots[slotIndex].NetworkObject != null && player.slots[slotIndex].NetworkObject.IsSpawned)
                {
                    player.SyncRestoredItemClientRpc(
                        new NetworkObjectReference(player.slots[slotIndex].NetworkObject),
                        slotIndex,
                        RpcTarget.Single(senderId, RpcTargetUse.Temp)
                    );
                }
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SyncRestoredItemClientRpc(NetworkObjectReference itemReference, int slotIndex, RpcParams rpcParams)
    {
        SyncRestoredItemClientRpc(itemReference, slotIndex);
    }

    [Rpc(SendTo.Everyone)]
    private void SyncRestoredItemClientRpc(NetworkObjectReference itemReference, int slotIndex)
    {
        if (itemReference.TryGet(out NetworkObject networkObj))
        {
            ItemBase item = networkObj.GetComponent<ItemBase>();
            slots[slotIndex] = item;
            SetItemPhysicsAndLayer(item, true);

            bool isWeapon = item.itemData.category == ItemCategory.Weapon;
            Transform targetHand = (item.itemData.handType == HandType.TwoHand && !isWeapon) ? bothHandsTransform : leftHandTransform;

            item.ExecuteChangeOwnership(true, targetHand);

            if (item.itemData.handType == HandType.TwoHand)
            {
                twoHandedItem = item;
                OnTwoHandedToggled?.Invoke(true);
            }

            if (slotIndex != currentSlotIndex && item.itemData.handType != HandType.TwoHand)
            {
                item.gameObject.SetActive(false);
            }
            else if (twoHandedItem != null && item != twoHandedItem)
            {
                item.gameObject.SetActive(false);
            }

            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
            RefreshQuestDebuffTiming();
        }
    }


    // ==========================================
    // 7. 저주 퀘스트 아이템 기믹 (Curse Gimmick)
    // ==========================================

    private void RefreshQuestDebuffTiming()
    {
        if (!IsOwner || QuestManager.Instance == null) { return; }

        bool applySpeedDebuff = false;
        bool applyAggro = false;
        bool applyHallucination = false;

        List<ItemBase> checkList = new List<ItemBase>(slots);
        if (twoHandedItem != null)
        {
            checkList.Add(twoHandedItem);
        }

        foreach (ItemBase item in checkList)
        {
            if (item == null) { continue; }
            int itemId = item.itemData.itemID;

            foreach (int questId in QuestManager.Instance.activeQuests)
            {
                QuestDataSO questData = QuestManager.Instance.GetQuestData(questId);
                if (questData != null && questData.targetItemID == itemId)
                {
                    if (questData.hasSpeedDebuff) applySpeedDebuff = true;
                    if (questData.hasMonsterAggro) applyAggro = true;
                    if (questData.hasHallucination) applyHallucination = true;
                }
            }
        }

        if (TryGetComponent(out PlayerMove playerMove))
        {
            playerMove.questSpeedMultiplier = applySpeedDebuff ? 0.75f : 1.0f;
        }

        hasCurseAggro = applyAggro;
        hasCurseHallucination = applyHallucination;

        if (applyAggro || applyHallucination)
        {
            if (curseRoutine == null)
            {
                curseRoutine = StartCoroutine(CurseEffectRoutine());
            }
        }
        else
        {
            if (curseRoutine != null)
            {
                StopCoroutine(curseRoutine);
                curseRoutine = null;
            }
        }
    }

    private IEnumerator CurseEffectRoutine()
    {
        WaitForSeconds waitDelay = new WaitForSeconds(7.0f);

        while (true)
        {
            yield return waitDelay;

            if (hasCurseAggro)
            {
                SoundManager.Instance.PlaySfxAndReportNoise(SfxSound.VENT_CREAK, transform.position, 1.0f);
                PlayCurseAggroSoundServerRpc();
            }

            if (hasCurseHallucination)
            {
                SoundManager.Instance.PlaySfx(SfxSound.ENV_RAIN);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void PlayCurseAggroSoundServerRpc()
    {
        PlayCurseAggroSoundClientRpc();
    }

    [Rpc(SendTo.NotMe)]
    private void PlayCurseAggroSoundClientRpc()
    {
        AudioClip clip = SoundManager.Instance.GetSfxClip(SfxSound.VENT_CREAK);
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }


    // ==========================================
    // 8. 유틸리티 (Utilities)
    // ==========================================

    private Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName)
            {
                return child;
            }
        }
        return null;
    }

    private void SetItemPhysicsAndLayer(ItemBase item, bool isEquipped)
    {
        if (item == null) { return; }

        Collider itemCollider = item.GetComponentInChildren<Collider>();
        if (itemCollider != null)
        {
            itemCollider.isTrigger = isEquipped;
        }

        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = isEquipped;
        }

        item.gameObject.layer = LayerMask.NameToLayer(isEquipped ? "EquippedItem" : "Item");
    }
}