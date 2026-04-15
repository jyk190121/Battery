using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnvironmentScanner))]
public class MonsterController : NetworkBehaviour
{
    [Header("--- Monster Configuration ---")]
    [Tooltip("몬스터의 기본 스탯 및 설정 데이터 (ScriptableObject)")]
    public MonsterData monsterData;

    [Header("--- Components & References ---")]
    [Tooltip("주변 환경 감지 시스템")]
    public EnvironmentScanner scanner;
    [Tooltip("네비게이션 에이전트")]
    public NavMeshAgent navAgent;
    [Tooltip("애니메이션 제어 핸들러")]
    public MonsterAnimation animHandler;
    [Tooltip("순찰 경로 매니저")]
    public WaypointManager waypointManager;

    [Header("--- Health System ---")]
    [Tooltip("몬스터의 현재 체력을 서버에서 관리하고 모든 클라이언트가 알 수 있게 합니다.")]
    public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
    0f,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    [Header("--- Network Variables (Synced) ---")]
    [Tooltip("현재 서버에서 동기화 중인 몬스터 상태")]
    public NetworkVariable<MonsterStateType> CurrentStateNet = new NetworkVariable<MonsterStateType>(
        MonsterStateType.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Tooltip("플레이어 감지 경계도 (0~1)")]
    public NetworkVariable<float> Alertness = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Tooltip("현재 빙결/스턴 상태 여부")]
    public NetworkVariable<bool> IsFrozenNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Header("--- Logic State (Local) ---")]
    [Tooltip("직전 상태 (상태 전환 로직 처리용)")]
    public MonsterStateType PreviousState;

    // 내부 캡슐화 변수들
    public DoorController TargetDoor { get; set; }
    private MonsterStateMachine stateMachine;
    private Animator _animator;
    private Dictionary<MonsterStateType, IState> states;

    // [최적화/추가 이유] 가비지 컬렉터(GC) 방지를 위한 NonAlloc용 캐시 배열
    private Collider[] doorHitColliders = new Collider[5];

    // [기믹 델리게이트] 외부 기믹 스크립트들이 멈춤 여부를 판별해주는 창구
    public delegate bool GimmickPauseCheck();
    [Header("--- Gimmick Events ---")]
    public GimmickPauseCheck OnCheckGimmickPause;
    private bool wasStoppedBeforeFreeze;

    private void Awake()
    {
        // [최적화/수정 이유] 서버/클라이언트 모두 로컬 FSM 인스턴스가 필요하므로 Awake에서 공통 초기화
        stateMachine = new MonsterStateMachine();

        // 상태 인스턴스 미리 생성 (Flyweight 패턴으로 메모리 재사용)
        states = new Dictionary<MonsterStateType, IState>
        {
            { MonsterStateType.Patrol, new PatrolState(this) },
            { MonsterStateType.Detect, new DetectState(this) },
            { MonsterStateType.Chase, new ChaseState(this) },
            { MonsterStateType.Search, new SearchState(this) },
            { MonsterStateType.Attack, new AttackState(this) },
            { MonsterStateType.InteractDoor, new InteractDoorState(this) },
            { MonsterStateType.Idle, new PatrolState(this) }
        };
    }

    public override void OnNetworkSpawn()
    {
        // 컴포넌트 자동 할당 및 초기화
        navAgent = GetComponent<NavMeshAgent>();
        scanner = GetComponent<EnvironmentScanner>();
        animHandler = GetComponentInChildren<MonsterAnimation>();
        if (animHandler != null) _animator = animHandler.GetComponentInChildren<Animator>();

        scanner.Init(this, monsterData);
        waypointManager = Object.FindAnyObjectByType<WaypointManager>();

        // [최적화/추가 이유] 네트워크 변수 변경 감지 이벤트 구독 (서버-클라이언트 싱크)
        CurrentStateNet.OnValueChanged += OnStateChangedCallback;
        IsFrozenNet.OnValueChanged += OnFrozenNetworkChanged;

        if (IsServer)
        {
            CurrentHealth.Value = monsterData.maxHealth;
            ChangeState(MonsterStateType.Patrol); // 초기 상태 설정
        }
        else
        {
            ApplyStateLocal(CurrentStateNet.Value); // 클라이언트 초기 싱크
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && EnemyManager.Instance != null)
        {
            EnemyManager.Instance.UnregisterEnemy(this.monsterData);
        }

        // 이벤트 구독 해제 (메모리 누수 방지)
        CurrentStateNet.OnValueChanged -= OnStateChangedCallback;
        IsFrozenNet.OnValueChanged -= OnFrozenNetworkChanged;

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        // [서버 전용 로직] 기믹 체크 및 스턴 관리
        if (IsServer)
        {
            HandleGimmickAndFrozenLogic();
        }

        // 빙결 상태라면 이후 AI 로직(FSM) 업데이트 중단
        if (IsFrozenNet.Value) return;

        // FSM 업데이트
        stateMachine?.Update();

        // 애니메이션 속도 업데이트 (서버/클라이언트 공통 시각적 효과)
        if (navAgent != null && animHandler != null)
        {
            // 실제 물리 속도(magnitude) 대신, 
            // 내비게이션 시스템이 내고자 하는 의도된 속도(desiredVelocity.magnitude)를 전달해 보세요.
            animHandler.SetVisualSpeed(
                navAgent.desiredVelocity.magnitude,
                monsterData.patrolSpeed,
                monsterData.chaseSpeed,
                CurrentStateNet.Value
            );
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || IsFrozenNet.Value) return;
        stateMachine?.FixedUpdate();
    }

    /// <summary>
    /// 서버에서 몬스터의 상태를 변경하고 모든 클라이언트에게 전파합니다.
    /// </summary>
    public void ChangeState(MonsterStateType newState)
    {
        if (!IsServer) return;
        if (CurrentStateNet.Value == newState) return;

        // NetworkVariable 변경 시 OnStateChangedCallback이 모든 유저에게 호출됨
        CurrentStateNet.Value = newState;
    }

    // --- 네트워크 콜백 및 내부 동기화 로직 ---

    private void OnStateChangedCallback(MonsterStateType previousValue, MonsterStateType newValue)
    {
        PreviousState = previousValue;
        ApplyStateLocal(newValue);

        // 공격 상태에서 다른 상태(Chase, Search 등)로 강제 전환되었다면 애니메이션 취소
        if (previousValue == MonsterStateType.Attack && newValue != MonsterStateType.Attack)
        {
            if (animHandler != null)
            {
                animHandler.CancelAttack();
            }
        }

        Debug.Log($"[Sync] {gameObject.name} State: {previousValue} -> {newValue}");
    }

    private void ApplyStateLocal(MonsterStateType newState)
    {
        if (states.TryGetValue(newState, out IState stateInstance))
        {
            stateMachine.ChangeState(stateInstance);
        }
    }

    private void OnFrozenNetworkChanged(bool previous, bool current)
    {
        // 애니메이터 속도 조절 (0이면 정지)
        if (_animator != null)
        {
            _animator.speed = current ? 0f : 1f;
        }
    }

    /// <summary>
    /// 외부 기믹(예: 손전등, 특정 아이템)에 의한 빙결 상태를 서버에서 계산합니다.
    /// </summary>
    private void HandleGimmickAndFrozenLogic()
    {
        bool shouldPause = false;
        if (OnCheckGimmickPause != null)
        {
            foreach (GimmickPauseCheck checkFunc in OnCheckGimmickPause.GetInvocationList())
            {
                if (checkFunc.Invoke()) { shouldPause = true; break; }
            }
        }

        if (IsFrozenNet.Value != shouldPause)
        {
            IsFrozenNet.Value = shouldPause;

            if (shouldPause)
            {
                wasStoppedBeforeFreeze = navAgent.isStopped;
                if (navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = true;
                    navAgent.velocity = Vector3.zero;
                }
            }
            else
            {
                // 1. 얼어붙음이 풀릴 때 원래 상태(정지/이동) 복구
                if (navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = wasStoppedBeforeFreeze;
                }

                // [추가된 핵심 로직] 만약 공격 중이었다면 거리를 재평가!
                if (CurrentStateNet.Value == MonsterStateType.Attack)
                {
                    Transform target = scanner.CurrentTarget;
                    if (target != null)
                    {
                        float sqrDist = (target.position - transform.position).sqrMagnitude;
                        float hitThreshold = monsterData.attackRange + 0.5f;

                        // 플레이어가 멀리 도망갔다면 공격을 즉시 취소하고 추격 시작
                        if (sqrDist > hitThreshold * hitThreshold)
                        {
                            // 서버에서 상태를 Chase로 변경하면, OnStateChangedCallback을 통해 모든 유저의 애니메이션이 취소됨
                            ChangeState(MonsterStateType.Chase);
                        }
                        // 여전히 사거리 안이라면 아무것도 안 함 -> 멈춰있던 AttackState가 마저 진행됨 (그대로 공격!)
                    }
                    else
                    {
                        // 타겟이 아예 시야에서 사라졌을 때
                        ChangeState(MonsterStateType.Search);
                    }
                }
            }
        }
    }

    // --- 헬퍼 및 전투 함수 ---

    public bool IsInSafeZone(GameObject obj)
    {
        return (1 << obj.layer & LayerMask.GetMask("SafeZone")) != 0;
    }

    public void ExecuteAttackDamage()
    {
        if (stateMachine.CurrentState is AttackState attackState)
        {
            attackState.ApplyDamageToTarget();
        }
    }

    /// <summary>
    /// 주변의 문을 탐색하고 상호작용 상태로 전환합니다. 
    /// </summary>
    public bool CheckAndHandleDoor()
    {
        if (!IsServer) return false;

        Vector3 checkPos = transform.position + (Vector3.up * 1.0f);
        int doorLayerMask = 1 << LayerMask.NameToLayer("Door");

        int hitCount = Physics.OverlapSphereNonAlloc(checkPos, 1.2f, doorHitColliders, doorLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = doorHitColliders[i];
            DoorController door = hit.GetComponentInParent<DoorController>();

            if (door != null && !door.isOpen)
            {
                Vector3 dirToDoor = (hit.bounds.center - transform.position).normalized;
                // 문이 전방 180도 내에 있다면 상호작용 시도
                if (Vector3.Dot(transform.forward, dirToDoor) > -0.2f)
                {
                    this.TargetDoor = door;
                    ChangeState(MonsterStateType.InteractDoor);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 외부(플레이어의 무기 등)에서 몬스터에게 데미지를 줄 때 호출하는 창구
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (!IsServer) return;

        if (CurrentStateNet.Value == MonsterStateType.Dead) return;

        CurrentHealth.Value -= damage;
        Debug.Log($"<color=red>[몬스터 피격]</color> {gameObject.name} 남은 체력: {CurrentHealth.Value}");

        // 체력이 0 이하라면 사망 상태로 전환
        if (CurrentHealth.Value <= 0)
        {
            CurrentHealth.Value = 0;
            ChangeState(MonsterStateType.Dead);
            return;
        }

        // 만약 머리에 붙어있는 상태(Attached)에서 맞았다면 즉시 도망(Flee) 상태로 전환
        if (CurrentStateNet.Value == MonsterStateType.Attached)
        {
            ChangeState(MonsterStateType.Flee);
        }
    }

    // [테스트용] 
    [ContextMenu("Test Damage (10)")]
    public void TestDamage()
    {
        if (IsServer) TakeDamage(10f);
    }
}