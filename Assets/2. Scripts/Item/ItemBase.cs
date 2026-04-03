using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
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
    }

    protected virtual void Start()
    {
        // 💡 [최적화] 시작할 때 멀티모드가 아니면 NetworkTransform을 꺼서 간섭 차단
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;
        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = isMulti;
    }

    public virtual void RequestDespawn()
    {
        // TODO: 멀티플레이 시 서버에서 NetworkObject.Despawn() 호출
        gameObject.SetActive(false);
    }

    // ==========================================================
    // 1. 소유권 변경 (줍기 / 버리기)
    // ==========================================================
    public virtual void RequestChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        // 💡 [이원화] 설정 파일의 멀티플레이 모드 체크
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (isMulti && IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // TODO: [ServerRpc] 멀티플레이 시 서버에 줍기/버리기 허락 요청
            ExecuteChangeOwnership(isPickingUp, targetHand);
        }
        else
        {
            // 싱글플레이 모드이거나 서버 연동 전이면 즉시 실행
            ExecuteChangeOwnership(isPickingUp, targetHand);
        }
    }

    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;

        // NGO 컴포넌트 참조 (에러 방지용 차단막)
        var netObj = GetComponent<NetworkObject>();
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (isPickingUp)
        {
            // 물리 제어
            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.linearVelocity = Vector3.zero;
                itemPhysicsRigidbody.isKinematic = true;
            }
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = false;

            // --- [NGO 진입 차단 if-else 구조] ---
            if (isMulti && IsSpawned)
            {
                // 💡 [True] 멀티플레이 모드일 때만 NGO 정석 로직 진행
                // TODO: 멀티플레이 전용 부모 설정 로직 (NetworkObject.TrySetParent 등)
                transform.SetParent(targetHand);
            }
            else
            {
                // 💡 [False] 싱글 모드일 때는 NGO가 감시하지 못하게 컴포넌트를 잠시 끄고 진행 (에러 원천 봉쇄)
                if (netObj != null) netObj.enabled = false;

                transform.SetParent(targetHand); // NGO 간섭 없이 일반 유니티 로직 실행

                if (netObj != null) netObj.enabled = true; // 다시 켜줌
            }
            // ------------------------------------

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (itemData != null)
                Debug.Log($"<color=#00FF00><b>[Anim Trigger]</b></color> {gameObject.name} 장착! -> {itemData.animType}");
        }
        else
        {
            // 버릴 때 (Drop)
            if (isMulti && IsSpawned)
            {
                // TODO: 멀티플레이 전용 부모 해제 로직
                transform.SetParent(null);
            }
            else
            {
                if (netObj != null) netObj.enabled = false;
                transform.SetParent(null);
                if (netObj != null) netObj.enabled = true;
            }

            if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = true;

            Debug.Log($"<color=#FF8800><b>[Anim Trigger]</b></color> {gameObject.name} 해제!");
        }
    }

    // ==========================================================
    // 2. 아이템 사용 (클릭)
    // ==========================================================
    public virtual void RequestUseItem()
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (isMulti && IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // TODO: [ServerRpc] 서버에 아이템 사용 요청
            ExecuteUseItem();
        }
        else
        {
            ExecuteUseItem();
        }
    }

    public virtual void ExecuteUseItem() { }

    // ==========================================================
    // 3. 기타 물리 로직
    // ==========================================================
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

    // ==========================================================
    // 4. 상태 저장/복구 (Data Persistence)
    // ==========================================================
    public virtual float[] ExtractSaveData() { return null; }
    public virtual void ApplySaveData(float[] savedStates) { }
}