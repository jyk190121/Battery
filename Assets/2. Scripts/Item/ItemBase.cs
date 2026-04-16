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

    protected Transform currentTargetHand;

    protected virtual void Awake()
    {
        itemPhysicsRigidbody = GetComponent<Rigidbody>();
        itemPhysicalCollider = GetComponent<Collider>();
    }

    protected virtual void Start() { }

    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;
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
            //if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = false;
            if (netTransform != null) netTransform.enabled = false;

            if (IsServer)
            {
                NetworkObject.TrySetParent(targetHand, false);
            }

            if (itemData != null) Debug.Log($"<color=green>[Execute]</color> {itemData.itemName} 장착 완료.");
        }
        else
        {
            if (IsServer)
            {
                NetworkObject.TryRemoveParent();
            }

            if (netTransform != null) netTransform.enabled = true;
            if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
            //if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = true;
        }
    }

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

    public virtual void RequestUseItem(Vector3 direction = default)
    {
        if (IsSpawned && IsOwner) RequestUseItemServerRpc(direction);
        else if (!IsSpawned) ExecuteUseItem(direction);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestUseItemServerRpc(Vector3 direction)
    {
        ExecuteUseItemClientRpc(direction);
    }

    [Rpc(SendTo.Everyone)]
    private void ExecuteUseItemClientRpc(Vector3 direction)
    {
        ExecuteUseItem(direction);
    }

    // 매개변수로 direction을 받도록 수정
    public virtual void ExecuteUseItem(Vector3 direction) { }
}