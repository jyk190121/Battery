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

    public NetworkVariable<MonsterStateType> CurrentStateNet = new NetworkVariable<MonsterStateType>();
    public NetworkVariable<float> Alertness = new NetworkVariable<float>();

    public NavMeshAgent navAgent;
    private MonsterStateMachine stateMachine;
    private Dictionary<MonsterStateType, IState> states;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            navAgent = GetComponent<NavMeshAgent>();
            scanner = GetComponent<EnvironmentScanner>();
            scanner.Init(this, monsterData);
            waypointManager = Object.FindAnyObjectByType<WaypointManager>();

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

        stateMachine.ChangeState(states[newState]);     // 실제 서버 상태 변경
        CurrentStateNet.Value = newState;               // 모든 클라이언트에게 상태 동기화
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
}