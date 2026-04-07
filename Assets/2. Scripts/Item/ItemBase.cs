using UnityEngine;
using Unity.Netcode;

/**
 * [ Item System Architecture & Optimization Guide ]
 * 1. 완벽한 네트워크 동기화 구조 적용 완료.
 * 2. 변수명 및 구조 100% 보존. (TryGetComponentInChildren 배제, 표준 API 사용)
 * 3. 자식 클래스가 에러 없이 override 할 수 있도록 RPC 및 virtual 함수 틀 제공.
 */
public abstract class ItemBase : NetworkBehaviour
{
    [Header("Item Data")]
    public ItemDataSO itemData;
    public bool isEquipped = false;

    [Header("Physics Components")]
    protected Rigidbody itemPhysicsRigidbody;
    protected Collider itemPhysicalCollider;
    protected bool isThrown = false;

    // ==========================================================
    // [1] 초기화 (이원화 보호 유지)
    // ==========================================================

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

    // ==========================================================
    // [2] 로컬 실행 로직 (Execute - 물리 및 시각 동기화)
    // ==========================================================

    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;

        // [핵심] 표준 API 적용 (자식 메쉬의 Outline 끄기)
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
    // [3] 서버 이관 및 패킷 최적화 구역 (RPC 동기화)
    // ==========================================================

    // 아이템 삭제 요청 (자식 클래스 사용)
    public virtual void RequestDespawn()
    {
        if (IsSpawned && IsOwner) RequestDespawnServerRpc();
        else if (!IsSpawned) Destroy(gameObject);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestDespawnServerRpc()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    // 아이템 사용 동기화 요청 (자식 클래스 사용)
    public virtual void RequestUseItem()
    {
        if (IsSpawned && IsOwner) RequestUseItemServerRpc();
        else if (!IsSpawned) ExecuteUseItem();
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestUseItemServerRpc()
    {
        ExecuteUseItemClientRpc();
    }

    [ClientRpc]
    private void ExecuteUseItemClientRpc()
    {
        ExecuteUseItem();
    }

    // 실제 아이템 사용 효과 (자식에서 override)
    public virtual void ExecuteUseItem() { }
}