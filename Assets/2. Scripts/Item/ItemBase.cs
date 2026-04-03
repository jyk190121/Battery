using UnityEngine;
using Unity.Netcode;

/**
 * [ Item System Architecture & Optimization Guide ]
 * * 1. 개발 및 테스트 로드맵 (Development Roadmap)
 * - [현재] 이원화 구조: 개발 편의를 위해 싱글 모드 시 NGO 컴포넌트를 자동 삭제하여 에러를 방지함.
 * - [추후] 1~4인 멀티 전용: 최종 배포 시 싱글 로직은 제거되며, 1인 플레이도 'Host' 세션으로 작동함.
 * * 2. 매니저 중심 패킷 최적화 (Manager-Centric & Packet Optimization)
 * - 대역폭 절감: 아이템 객체 전체를 네트워크로 전송하지 않고, 8바이트 숫자 고유값인 [NetworkObjectId]만 전송함.
 * - 중앙 제어: 모든 상호작용 판정(권한 체크, 거리 체크)은 서버의 ItemManager에서 ID 기반으로 일괄 처리함.
 * * 3. 권한 및 실행의 분리 (Request-Execute Pattern)
 * - Request_ : 서버에 행위 허가를 요청 (티켓 창구 역할).
 * - Execute_ : 서버 승인 후 모든 클라이언트에서 실제 물리/시각적 변화를 실행 (결과 반영).
 */

public abstract class ItemBase : NetworkBehaviour
{
    [Header("Item Data")]
    [Tooltip("사용자 정의 ItemDataSO (itemID, Category, SpawnLocation 등 포함)")]
    public ItemDataSO itemData;
    public bool isEquipped = false;

    [Header("Physics Components")]
    protected Rigidbody itemPhysicsRigidbody;
    protected Collider itemPhysicalCollider;
    protected bool isThrown = false;

    // ==========================================================
    // [1] 초기화 및 이원화 보호 (Initialization)
    // ==========================================================

    protected virtual void Awake()
    {
        itemPhysicsRigidbody = GetComponent<Rigidbody>();
        itemPhysicalCollider = GetComponent<Collider>();

        // 💡 [안정성] 싱글 모드 테스트 시 NGO 내부 에러(NullRef) 방지를 위한 원천 차단
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (!isMulti)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // 싱글 모드일 때는 NGO가 없는 것처럼 동작하게 삭제 (프리팹은 안전함)
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
    // 💡 서버의 승인 신호가 떨어진 후 "최종 결과"를 모든 클라에 뿌리는 구간입니다.
    // ==========================================================

    public virtual void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;

        if (isPickingUp)
        {
            // [강건화] 물리 엔진 간섭 초기화
            if (itemPhysicsRigidbody != null)
            {
                itemPhysicsRigidbody.linearVelocity = Vector3.zero;
                itemPhysicsRigidbody.angularVelocity = Vector3.zero;
                itemPhysicsRigidbody.isKinematic = true;
            }
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = false;

            // 소유권 시각적 반영 (부모 설정)
            transform.SetParent(targetHand);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (itemData != null)
                Debug.Log($"<color=green>[Execute]</color> {itemData.itemName} 장착 완료.");
        }
        else
        {
            // 아이템 버리기/해제
            transform.SetParent(null);

            if (itemPhysicsRigidbody != null) itemPhysicsRigidbody.isKinematic = false;
            if (itemPhysicalCollider != null) itemPhysicalCollider.enabled = true;
        }
    }

    public virtual void ExecuteUseItem() { /* 자식 클래스(손전등, 무기 등)에서 구현 */ }

    public virtual void BeginThrownState() { isThrown = true; }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        // 바닥에 닿으면 투척 상태 해제 및 물리 정지
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

    // 데이터 저장/로드용 관문
    public virtual float[] ExtractSaveData() { return null; }
    public virtual void ApplySaveData(float[] savedStates) { }


    //-----------------------------------------------------------
    // [3] 서버 이관 및 패킷 최적화 구역 (Networking Interface)
    // 💡 이 아래는 1~4인 멀티플레이 시 중앙 매니저(Host)와 통신하는 통로입니다.
    // 💡 최적화: 객체 참조 대신 [NetworkObjectId] ulong 값을 사용하십시오.
    //-----------------------------------------------------------

    public virtual void RequestDespawn()
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        // 1~4인 멀티 시에는 1인 플레이도 Host 세션이므로 IsSpawned가 true가 됩니다.
        if (isMulti && IsSpawned)
        {
            // TODO: [Optimization] ItemManager.Instance.RequestDespawnServerRpc(this.NetworkObjectId);
            gameObject.SetActive(false);
        }
        else
        {
            // 개발용 싱글 모드일 때는 즉시 파괴
            Destroy(gameObject);
        }
    }

    public virtual void RequestChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (isMulti && IsSpawned)
        {
            // TODO: [Optimization] 아이템 객체 대신 내 ID만 서버 매니저에게 전달
            // ItemManager.Instance.RequestPickupServerRpc(this.NetworkObjectId, myPlayerId);

            // 현재는 테스트를 위해 즉시 실행 로직 유지
            ExecuteChangeOwnership(isPickingUp, targetHand);
        }
        else
        {
            // 싱글 모드
            ExecuteChangeOwnership(isPickingUp, targetHand);
        }
    }

    public virtual void RequestUseItem()
    {
        bool isMulti = NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

        if (isMulti && IsSpawned)
        {
            // TODO: 서버 매니저에게 사용 승인 요청 -> 승인 시 모든 클라에서 ExecuteUseItem 실행
            ExecuteUseItem();
        }
        else
        {
            ExecuteUseItem();
        }
    }
}