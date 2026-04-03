using UnityEngine;
using Unity.Netcode;

public abstract class ItemBase : NetworkBehaviour
{
    [Header("Base Data")]
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

        // 💡 [최후의 수단: NGO 원천 봉쇄]
        // 멀티플레이 모드가 아니거나, 씬에 설정 오브젝트가 없다면 NGO 컴포넌트를 삭제합니다.
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (!isMulti)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // 싱글 모드일 때는 NGO가 아예 존재하지 않는 것처럼 삭제해버립니다.
                // (프리팹은 안전하며, 실시간 생성된 인스턴스에서만 사라집니다.)
                DestroyImmediate(netObj);
                Debug.Log($"<color=cyan><b>[Single Mode]</b></color> {gameObject.name}의 NetworkObject를 제거하여 에러를 차단했습니다.");
            }
        }
    }

    protected virtual void Start()
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;
        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = isMulti;
    }

    public virtual void RequestDespawn()
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;
        if (isMulti && IsSpawned)
        {
            // TODO: [ServerRpc] 멀티플레이 Despawn
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public virtual void RequestChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        // NGO 컴포넌트가 살아있고 멀티 모드일 때만 NGO 로직 수행
        if (isMulti && GetComponent<NetworkObject>() != null && IsSpawned)
        {
            // TODO: [ServerRpc] 멀티플레이 권한 요청
            ExecuteChangeOwnership(isPickingUp, targetHand);
        }
        else
        {
            ExecuteChangeOwnership(isPickingUp, targetHand);
        }
    }

    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;

        if (isPickingUp)
        {
            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.linearVelocity = Vector3.zero;
                itemPhysicsRigidbody.isKinematic = true;
            }
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = false;

            // 💡 [핵심] 이제 NGO 간섭 없이 안전하게 부모를 설정합니다.
            transform.SetParent(targetHand);

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.SetParent(null);

            if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = true;
        }
    }

    public virtual void RequestUseItem() { ExecuteUseItem(); }
    public virtual void ExecuteUseItem() { }
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
}