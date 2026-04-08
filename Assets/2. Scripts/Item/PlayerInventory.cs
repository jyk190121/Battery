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

    public float throwForce = 1f;

    public Action<int> OnSlotChanged;
    public Action OnInventoryUpdated;
    public Action<bool> OnTwoHandedToggled;

    private ItemBase lastLookedItem;
    private DepartureButton lastLookedButton;
    private MissionStartButton lastLookedMissionButton;

    public override void OnNetworkSpawn()
    {
        leftHandTransform = FindChildByName(transform, leftHandName);
        bothHandsTransform = FindChildByName(transform, bothHandsName);

        if (leftHandTransform == null || bothHandsTransform == null)
            Debug.LogError($"[PlayerInventory] 손 Transform을 찾지 못했습니다!");

        if (IsOwner) LocalInstance = this;

        if (IsServer)
        {
            StartCoroutine(WaitOneFrameAndRestore());
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
        else if (IsOwner)
        {
            // 💡 [보완 4] 난입 유저를 위한 시각적 동기화 요청
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
        HandleSlotChange();

        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                if (lastLookedButton != null) lastLookedButton.Interact(this);
                else if (lastLookedMissionButton != null) lastLookedMissionButton.Interact(this);
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
        lastLookedMissionButton = null;
    }

    private void TryPickUpAction()
    {
        if (lastLookedItem != null && twoHandedItem == null && !lastLookedItem.isEquipped)
        {
            // 가방 Full 상태 사전 예외 처리
            bool hasEmptySlot = false;
            foreach (var slot in slots) if (slot == null) hasEmptySlot = true;

            if (!hasEmptySlot)
            {
                Debug.LogWarning("가방이 꽉 차서 주울 수 없습니다!");
                return;
            }

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

            //  벽 뚫기 방지 레이캐스트 검사
            Vector3 dropPos = startPos + throwDir * 1.5f;
            if (Physics.Raycast(startPos, throwDir, out RaycastHit hit, 1.5f))
            {
                // 플레이어가 아닌 벽에 맞았다면, 벽 바로 앞에 떨어지게 보정
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

    // ==========================================================
    // 💡 [보완 4] 난입 유저 동기화 로직 구역
    // ==========================================================
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestSyncLateJoinerServerRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // 서버가 접속해 있는 모든 플레이어의 아이템 소유 상태를 뉴비에게 전달
        PlayerInventory[] allPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            for (int i = 0; i < p.slots.Length; i++)
            {
                if (p.slots[i] != null && p.slots[i].NetworkObject != null && p.slots[i].NetworkObject.IsSpawned)
                {
                    // 뉴비(Target)에게만 강제로 동기화 RPC 쏘기
                    p.SyncRestoredItemClientRpc(new NetworkObjectReference(p.slots[i].NetworkObject), i, RpcTarget.Single(senderId, RpcTargetUse.Temp));
                }
            }
        }
    }

    // 오버로딩 (특정 클라이언트에게만 쏠 수 있는 타겟 RPC)
    [Rpc(SendTo.SpecifiedInParams)]
    private void SyncRestoredItemClientRpc(NetworkObjectReference itemRef, int slotIdx, RpcParams rpcParams)
    {
        SyncRestoredItemClientRpc(itemRef, slotIdx); // 기존 함수 재활용
    }
}