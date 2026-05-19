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

    // [저주 아이템 상태 관리용 변수]
    private Coroutine curseRoutine = null;
    private bool currentHasAggro = false;
    private bool currentHasHallucination = false;

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

    private void SetItemPhysicsAndLayer(ItemBase item, bool equipped)
    {
        if (item == null) return;

        Collider col = item.GetComponentInChildren<Collider>();
        if (col != null)
        {
            col.isTrigger = equipped;
        }

        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = equipped;
        }

        item.gameObject.layer = LayerMask.NameToLayer(equipped ? "EquippedItem" : "Item");
    }

    void Update()
    {
        if (!IsOwner) return;

        if (isControlLocked) return;
        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isPhoneActive) return;

        CheckInteraction();

        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                if (lastLookedButton != null) lastLookedButton.Interact(this);
                else if (lastLookedReturnPoint != null) lastLookedReturnPoint.Interact(this);
                else if (lastLookedDoor != null)
                {
                   lastLookedDoor.TryOpen();
                }
                else if (lastLookedItem != null) TryPickUpAction();
            }

            if (Keyboard.current[Key.G].wasPressedThisFrame)
            {
                RequestDropCurrentItem();
            }
        }

        HandleSlotChange();
    }

    #region 변경점
    void HandleSlotChange()
    {
        if (twoHandedItem != null) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;

        int newIndex = currentSlotIndex;
        if (scroll < 0f && newIndex < slots.Length - 1) newIndex++;
        else if (scroll > 0f && newIndex > 0) newIndex--;

        if (newIndex != currentSlotIndex) RequestChangeSlotServerRpc(newIndex);
    }

    [Rpc(SendTo.Everyone)]
    void SyncSlotChangeClientRpc(int newIndex)
    {
        if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(false);
        currentSlotIndex = newIndex;
        ItemBase newHeldItem = slots[currentSlotIndex];
        if (newHeldItem != null) newHeldItem.gameObject.SetActive(true);

        if (TryGetComponent(out PlayerEquipment equipment)) equipment.OnSlotItemChanged(newHeldItem);

        if (IsOwner) OnSlotChanged?.Invoke(currentSlotIndex);
    }
    #endregion

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
                return;
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

        SetItemPhysicsAndLayer(item, true);

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
        RefreshQuestDebuffTiming();
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

    public void ForceDropCurrentItemServer()
    {
        if (!IsServer) return;

        ItemBase itemToDrop = HeldItem;

        if (itemToDrop != null)
        {
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

        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            RefreshQuestDebuffTiming();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestChangeSlotServerRpc(int newIndex) { SyncSlotChangeClientRpc(newIndex); }

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
            RefreshQuestDebuffTiming();
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

        ItemBase itemToRemove = null;
        int slotIdx = -1;
        bool isTwoHand = false;

        if (twoHandedItem != null && twoHandedItem.itemData.itemID == itemID)
        {
            itemToRemove = twoHandedItem;
            isTwoHand = true;
        }
        else
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].itemData.itemID == itemID)
                {
                    itemToRemove = slots[i];
                    slotIdx = i;
                    break;
                }
            }
        }

        if (itemToRemove != null)
        {
            NotifySyncItemRemovedClientRpc(slotIdx, isTwoHand);

            if (itemToRemove.NetworkObject != null && itemToRemove.NetworkObject.IsSpawned)
            {
                itemToRemove.NetworkObject.Despawn();
            }
            return true;
        }

        return false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestRemoveItemServerRpc(int itemID)
    {
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
            slots[slotIdx] = null;
        }

        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
            RefreshQuestDebuffTiming();
        }
    }

    public void DropAllItemsOnDeathServer()
    {
        if (!IsServer) return;

        Vector3 dropOrigin = transform.position + Vector3.up * 0.8f;

        if (twoHandedItem != null)
        {
            Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.5f, 1.5f), UnityEngine.Random.Range(-1f, 1f)).normalized;
            ForceDropItem(twoHandedItem, dropOrigin, randomDir);
        }

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
            item.NetworkObject.RemoveOwnership();
            NotifyItemDroppedClientRpc(item.NetworkObjectId, pos, dir);
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

        if (IsOwner)
        {
            OnInventoryUpdated?.Invoke();
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    // ==========================================
    // [저주 기믹 연동부]
    // ==========================================
    private void RefreshQuestDebuffTiming()
    {
        if (!IsOwner || QuestManager.Instance == null) return;

        bool applySpeedDebuff = false;
        bool applyAggro = false;
        bool applyHallucination = false;

        List<ItemBase> checkList = new List<ItemBase>(slots);
        if (twoHandedItem != null) checkList.Add(twoHandedItem);

        foreach (var item in checkList)
        {
            if (item == null) continue;
            int id = item.itemData.itemID;

            // 중앙 퀘스트 매니저를 통해 기획서 데이터(QuestDataSO) 동적 참조
            foreach (int qId in QuestManager.Instance.activeQuests)
            {
                QuestDataSO qData = QuestManager.Instance.GetQuestData(qId);
                if (qData != null && qData.targetItemID == id)
                {
                    if (qData.hasSpeedDebuff) applySpeedDebuff = true;
                    if (qData.hasMonsterAggro) applyAggro = true;
                    if (qData.hasHallucination) applyHallucination = true;
                }
            }
        }

        // 1. 이동속도 디버프 즉시 적용
        if (TryGetComponent(out PlayerMove move))
        {
            move.questSpeedMultiplier = applySpeedDebuff ? 0.75f : 1.0f;
            Debug.Log("<color=blue>[Curse]</color> 이동속도 제한.");
        }

        // 2. 코루틴을 이용한 어그로/환청 제어
        currentHasAggro = applyAggro;
        currentHasHallucination = applyHallucination;

        if (applyAggro || applyHallucination)
        {
            if (curseRoutine == null) curseRoutine = StartCoroutine(CurseEffectRoutine());
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

    private System.Collections.IEnumerator CurseEffectRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(7.0f); // 기획에 맞춰 주기 조절

        while (true)
        {
            yield return wait;

            if (currentHasAggro)
            {
                // [핵심] SoundManager의 몬스터 호출 함수를 그대로 사용
                // 내 컴퓨터에서 3D 사운드를 재생하고, 몬스터 매니저에게 소음을 신고함
                SoundManager.Instance.PlaySfxAndReportNoise(SfxSound.VENT_CREAK, transform.position, 1.0f);

                // 다른 팀원들도 이 공포스러운 소리를 들어야 하므로 사운드 동기화 지시
                PlayCurseAggroSoundServerRpc();

                Debug.Log("<color=red>[Curse]</color> 소지한 저주 아이템이 괴물을 부릅니다!");
            }

            if (currentHasHallucination)
            {
                // [핵심] 오직 '나(Owner)'에게만 들리는 로컬 2D 사운드 재생
                SoundManager.Instance.PlaySfx(SfxSound.ENV_RAIN);
                Debug.Log("<color=purple>[Curse]</color> 등 뒤에서 발소리가 들린 것 같다...");
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void PlayCurseAggroSoundServerRpc()
    {
        // 서버를 거쳐 '나를 제외한 나머지 팀원들'에게만 사운드 재생 명령을 내림
        PlayCurseAggroSoundClientRpc();
    }

    // SendTo.NotMe: 나는 이미 PlaySfxAndReportNoise로 소리를 들었으므로 제외함
    [Rpc(SendTo.NotMe)]
    private void PlayCurseAggroSoundClientRpc()
    {
        // 팀원들의 컴퓨터에서는 몬스터에게 소음을 이중 신고하지 않도록 순수 사운드만 재생함
        AudioClip clip = SoundManager.Instance.GetSfxClip(SfxSound.VENT_CREAK);
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }


    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestClearSlotServerRpc(int slotIndex)
    {
        // 서버에서 해당 슬롯을 비웁니다.
        if (slotIndex >= 0 && slotIndex < slots.Length)
        {
            slots[slotIndex] = null;
            // 클라이언트들에게도 이 슬롯이 비었음을 알립니다 (필요 시)
            ClearSlotClientRpc(slotIndex);
        }
    }

    [ClientRpc]
    private void ClearSlotClientRpc(int slotIndex)
    {
        if (!IsServer) slots[slotIndex] = null;
    }
}