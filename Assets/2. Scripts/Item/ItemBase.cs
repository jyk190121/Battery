using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public abstract class ItemBase : NetworkBehaviour
{
    [Header("Item Data")]
    public ItemDataSO itemData;
    public bool isEquipped = false;

    [Header("Physics Components")]
    protected Rigidbody itemPhysicsRigidbody;
    protected Collider itemPhysicalCollider;
    protected bool isThrown = false;

    // 💡 [추가됨] 강제 위치 추적을 위한 타겟 손 변수
    protected Transform currentTargetHand;

    protected virtual void Awake()
    {
        itemPhysicsRigidbody = GetComponent<Rigidbody>();
        itemPhysicalCollider = GetComponent<Collider>();

        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (!isMulti)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null)
            {
                DestroyImmediate(netObj);
                Debug.Log($"<color=cyan><b>[Dev-Single]</b></color> {gameObject.name} NGO 차단 완료.");
            }
        }
    }

    protected virtual void Start()
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;
        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = isMulti;
    }

    // 💡 [해결 2] 덜덜거림을 유발하던 SnapToHandRoutine 코루틴을 완전히 삭제했습니다.
    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;

        // 타겟 손 저장
        currentTargetHand = isPickingUp ? targetHand : null;

        Outline outline = GetComponentInChildren<Outline>();
        if (outline != null) outline.enabled = false;

        var netTransform = GetComponent<NetworkTransform>();

        if (isPickingUp)
        {
            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.linearVelocity = Vector3.zero;
                itemPhysicsRigidbody.angularVelocity = Vector3.zero;
                itemPhysicsRigidbody.isKinematic = true;
            }
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = false;

            // 줍는 순간 NetworkTransform 동기화를 꺼서 위치 싸움을 막습니다.
            if (netTransform != null) netTransform.enabled = false;

            if (IsServer)
            {
                NetworkObject.TrySetParent(targetHand, false);
            }

            if (itemData != null)
                Debug.Log($"<color=green>[Execute]</color> {itemData.itemName} 장착 완료.");
        }
        else
        {
            if (IsServer)
            {
                NetworkObject.TryRemoveParent();
            }

            if (netTransform != null) netTransform.enabled = true;
            if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = true;
        }
    }

    // 💡 [해결 2 핵심] 클라이언트에서 NGO가 부모를 어떻게 꼬아놓든 무시하고, 무조건 손 위치를 강제로 따라갑니다.
    protected virtual void Update()
    {
        if (isEquipped && currentTargetHand != null)
        {
            transform.position = currentTargetHand.position;
            transform.rotation = currentTargetHand.rotation;
        }
    }

    public virtual void BeginThrownState() { isThrown = true; }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (isThrown && collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.linearVelocity = Vector3.zero;
                itemPhysicsRigidbody.angularVelocity = Vector3.zero;
            }
            isThrown = false;
        }
    }

    public virtual float[] ExtractSaveData() { return null; }
    public virtual void ApplySaveData(float[] savedStates) { }

    public virtual void RequestDespawn()
    {
        if (IsSpawned && IsOwner) RequestDespawnServerRpc();
        else if (!IsSpawned) Destroy(gameObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestDespawnServerRpc()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    public virtual void RequestUseItem()
    {
        if (IsSpawned && IsOwner) RequestUseItemServerRpc();
        else if (!IsSpawned) ExecuteUseItem();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestUseItemServerRpc()
    {
        ExecuteUseItemClientRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ExecuteUseItemClientRpc()
    {
        ExecuteUseItem();
    }

    public virtual void ExecuteUseItem() { }
}