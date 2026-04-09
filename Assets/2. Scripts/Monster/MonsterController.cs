using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Unity.Netcode.Components.AttachableBehaviour;

public class MonsterController : NetworkBehaviour
{
    public MonsterData monsterData;
    public EnvironmentScanner scanner;
    public MonsterAnimation animHandler; // 별도 분리된 애니메이션 클래스
    public WaypointManager waypointManager;
    public DoorController TargetDoor { get; set; }

    public NetworkVariable<MonsterStateType> CurrentStateNet = new NetworkVariable<MonsterStateType>(MonsterStateType.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> Alertness = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NavMeshAgent navAgent;
    private MonsterStateMachine stateMachine;
    public MonsterStateType PreviousState;
    private Dictionary<MonsterStateType, IState> states;

    public override void OnNetworkSpawn()
    {
        navAgent = GetComponent<NavMeshAgent>();
        scanner = GetComponent<EnvironmentScanner>();
        animHandler = GetComponentInChildren<MonsterAnimation>();
        scanner.Init(this, monsterData);

        waypointManager = Object.FindAnyObjectByType<WaypointManager>();

        if (IsServer)
        {
            //navAgent = GetComponent<NavMeshAgent>();
            //scanner = GetComponent<EnvironmentScanner>();
            //scanner.Init(this, monsterData);
            //waypointManager = Object.FindAnyObjectByType<WaypointManager>();

            // 상태 인스턴스 생성 및 저장
            states = new Dictionary<MonsterStateType, IState>
            {
                { MonsterStateType.Patrol, new PatrolState(this) },
                { MonsterStateType.Detect, new DetectState(this) },
                { MonsterStateType.Chase, new ChaseState(this) },
                { MonsterStateType.Search, new SearchState(this) },
                { MonsterStateType.Attack, new AttackState(this) },
                { MonsterStateType.InteractDoor, new InteractDoorState(this) }
            };

            stateMachine = new MonsterStateMachine();
            ChangeState(MonsterStateType.Patrol); // 시작 상태
        }
        // 클라이언트에서 상태가 변했을 때 애니메이션/이펙트를 동기화하기 위한 콜백 연결
        CurrentStateNet.OnValueChanged += OnStateChangedCallback;
    }

    public override void OnNetworkDespawn()
    {
        CurrentStateNet.OnValueChanged -= OnStateChangedCallback;
    }

    private void Update()
    {
        if (!IsServer) return;
        stateMachine.Update();
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        stateMachine.FixedUpdate();

        // 클라이언트를 위한 데이터 동기화 (최적화를 위해 변화가 있을 때만 처리 가능)
        // 예: animHandler.UpdateParams(navAgent.velocity.magnitude, Alertness.Value);
    }

    public void ChangeState(MonsterStateType newState)
    {
        if (!IsServer) return;
        PreviousState = CurrentStateNet.Value;
        stateMachine.ChangeState(states[newState]);     // 실제 서버 상태 변경
        CurrentStateNet.Value = newState;               // 모든 클라이언트에게 상태 동기화
    }

    // 상태 변경 시 클라이언트와 서버 모두 호출되는 이벤트
    private void OnStateChangedCallback(MonsterStateType previousValue, MonsterStateType newValue)
    {
        // 예: 수색 상태 진입 시 클라이언트에서도 기괴한 사운드 재생, 애니메이션 플래그 세팅 등
        // animHandler를 여기서 제어하여 네트워크 대역폭(RPC)을 절약할 수 있습니다.
        Debug.Log($"[네트워크 동기화] 몬스터 상태 변경: {previousValue} -> {newValue}");
    }

    public bool IsInSafeZone(GameObject obj)
    {
        // 레이어 체크 또는 트리거 체크 로직
        return (1 << obj.layer & LayerMask.GetMask("SafeZone")) != 0;
    }

    public void ExecuteAttackDamage()
    {
        // 현재 상태가 AttackState인지 확인
        if (stateMachine.CurrentState is AttackState attackState)
        {
            attackState.ApplyDamageToTarget();
        }
    }

    // 문 감지 함수
    //public bool CheckAndHandleDoor()
    //{
    //    RaycastHit hit;
    //    Vector3 rayStart = transform.position + Vector3.up * 1.5f;

    //    // 레이 길이 2.5m (문 근처에 도달했을 때 감지)
    //    if (Physics.Raycast(rayStart, transform.forward, out hit, 2.5f))
    //    {
    //        // 레이어 이름이 "Door"인 경우
    //        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Door"))
    //        {
    //            DoorController door = hit.collider.GetComponentInParent<DoorController>();
    //            if (door != null && !door.isOpen)
    //            {
    //                this.TargetDoor = door;
    //                ChangeState(MonsterStateType.InteractDoor);
    //                return true; // 문을 발견해서 상태를 전환
    //            }
    //        }
    //    }
    //    return false;
    //}

    public bool CheckAndHandleDoor()
    {
        // [수정] 가슴 앞쪽(transform.forward)이 아니라, 몬스터의 중심을 기준으로 잡습니다.
        Vector3 centerPos = transform.position + (Vector3.up * 1.0f);
        int doorLayerMask = 1 << LayerMask.NameToLayer("Door");

        // [수정] 반경 1.5m 짜리 넓은 구체를 몬스터 몸을 감싸듯 생성합니다 (전후좌우 모두 감지)
        Collider[] hitColliders = Physics.OverlapSphere(centerPos, 1.5f, doorLayerMask);

        if (hitColliders.Length > 0)
        {
            DoorController door = hitColliders[0].GetComponentInParent<DoorController>();
            if (door != null && !door.isOpen)
            {
                this.TargetDoor = door;
                ChangeState(MonsterStateType.InteractDoor);
                return true;
            }
        }
        return false;
    }
}