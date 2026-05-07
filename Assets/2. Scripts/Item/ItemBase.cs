using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Splines;

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

    [Header("Grip Settings (장착 위치 보정)")]
    public Vector3 gripPositionOffset = Vector3.zero;
    public Vector3 gripRotationOffset = Vector3.zero;

    protected virtual void Awake()
    {
        itemPhysicsRigidbody = GetComponent<Rigidbody>();
        itemPhysicalCollider = GetComponent<Collider>();
    }

    protected virtual void Start() { }
    private void OnEnable()
    {
        if (isEquipped && currentTargetHand != null)
        {
            ForceSnapPosition();
        }
    }
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

            // 네트워크 변환 동기화 일시 중지 (손 위치 강제 추적을 위함)
            if (netTransform != null) netTransform.enabled = false;

            ForceSnapPosition();

            if (itemData != null) Debug.Log($"<color=green>[Execute]</color> {itemData.itemName} 장착 완료.");
        }
        else
        {
            if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
            if (netTransform != null)
            {
                // 중요: Teleport는 '서버' 권한이 있는 쪽에서만 호출합니다.
                if (IsServer)
                {
                    // 현재 transform 위치로 동기화 위치를 강제 설정합니다.
                    netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
                }

                // NetworkTransform을 다시 켜서 동기화를 재개합니다. (모든 클라이언트 공통)
                netTransform.enabled = true;
            }

        //if (netTransform != null)
        //    {
        //        netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        //        netTransform.enabled = true;
        //    }
        //    if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
        }
    }
    private void ForceSnapPosition()
    {
        if (currentTargetHand != null)
        {
            transform.position = currentTargetHand.TransformPoint(gripPositionOffset);
            transform.rotation = currentTargetHand.rotation * Quaternion.Euler(gripRotationOffset);
        }
    }


    // 애니메이션 덜덜거림 방지를 위해 Update 대신 LateUpdate 사용
    protected virtual void LateUpdate()
    {
        if (isEquipped && currentTargetHand != null)
        {
            transform.position = currentTargetHand.TransformPoint(gripPositionOffset);
            transform.rotation = currentTargetHand.rotation * Quaternion.Euler(gripRotationOffset);
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

    public virtual void ExecuteUseItem(Vector3 direction) { }
}