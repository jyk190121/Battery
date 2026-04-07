using UnityEngine;
using Unity.Netcode;

public abstract class ItemBase : NetworkBehaviour
{
    [Header("Item Data")]
    public ItemDataSO itemData;
    public bool isEquipped = false;

    [Header("Physics Components")]
    protected Rigidbody itemPhysicsRigidbody;
    protected Collider itemPhysicalCollider;
    protected bool isThrown = false;

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

    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;

        Outline outline = GetComponentInChildren<Outline>();
        if (outline != null) outline.enabled = false;

        if (isPickingUp)
        {
            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.linearVelocity = Vector3.zero;
                itemPhysicsRigidbody.angularVelocity = Vector3.zero;
                itemPhysicsRigidbody.isKinematic = true;
            }
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = false;

            transform.SetParent(targetHand);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (itemData != null)
                Debug.Log($"<color=green>[Execute]</color> {itemData.itemName} 장착 완료.");
        }
        else
        {
            transform.SetParent(null);
            if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = true;
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

    // ==========================================================
    // [RPC 최신 문법 적용 구역]
    // ==========================================================

    public virtual void RequestDespawn()
    {
        if (IsSpawned && IsOwner) RequestDespawnServerRpc();
        else if (!IsSpawned) Destroy(gameObject);
    }

    // [경고 해결] RequireOwnership은 삭제되고 InvokePermission으로 대체됨
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