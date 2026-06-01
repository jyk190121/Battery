using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 게임 내 모든 아이템의 물리 엔진, 네트워크 소유권, 장착 및 동기화 상태를 관리하는 최상위 부모 클래스입니다.
/// </summary>
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

    private NetworkTransform networkTransform;


    // ==========================================
    // 1. 생명주기 및 초기화 (Lifecycle)
    // ==========================================

    protected virtual void Awake()
    {
        itemPhysicsRigidbody = GetComponent<Rigidbody>();
        itemPhysicalCollider = GetComponent<Collider>();

        TryGetComponent(out networkTransform);
    }

    protected virtual void Start() { }

    private void OnEnable()
    {
        if (isEquipped && currentTargetHand != null)
        {
            ForceSnapPosition();
        }
    }

    protected virtual void LateUpdate()
    {
        if (isEquipped && currentTargetHand != null)
        {
            transform.position = currentTargetHand.TransformPoint(gripPositionOffset);
            transform.rotation = currentTargetHand.rotation * Quaternion.Euler(gripRotationOffset);
        }
    }


    // =====================
    // 2. 장착 및 물리 제어 
    // =====================

    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;
        currentTargetHand = isPickingUp ? targetHand : null;

        Outline outline = GetComponentInChildren<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        if (isPickingUp)
        {
            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.linearVelocity = Vector3.zero;
                itemPhysicsRigidbody.angularVelocity = Vector3.zero;
                itemPhysicsRigidbody.isKinematic = true;
            }

            if (networkTransform != null)
            {
                networkTransform.enabled = false;
            }

            ForceSnapPosition();

            if (itemData != null)
            {
                Debug.Log($"<color=green>[Execute]</color> {itemData.itemName} 장착 완료.");
            }
        }
        else
        {
            transform.SetParent(null);

            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.isKinematic = false;
                itemPhysicsRigidbody.WakeUp();
            }

            if (networkTransform != null)
            {
                if (IsServer)
                {
                    networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
                }
                networkTransform.enabled = true;
            }
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

    public virtual void BeginThrownState()
    {
        isThrown = true;
    }

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


    // ========================
    // 3. 네트워크 및 사용 연동 
    // ========================

    public virtual float[] ExtractSaveData() { return null; }

    public virtual void ApplySaveData(float[] savedStates) { }

    public virtual void RequestDespawn()
    {
        if (IsSpawned && IsOwner)
        {
            RequestDespawnServerRpc();
        }
        else if (!IsSpawned)
        {
            Destroy(gameObject);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestDespawnServerRpc()
    {
        if (NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    public virtual void RequestUseItem(Vector3 direction = default)
    {
        if (IsSpawned && IsOwner)
        {
            RequestUseItemServerRpc(direction);
        }
        else if (!IsSpawned)
        {
            ExecuteUseItem(direction);
        }
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