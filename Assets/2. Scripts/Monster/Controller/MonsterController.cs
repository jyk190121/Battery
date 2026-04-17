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
    public float CurrentStunDuration { get; private set; } = 0f;
    private MonsterStateMachine stateMachine;
    private Animator _animator;
    private Dictionary<MonsterStateType, IState> states;

    private float serverAlertness = 0f;
    private float lastSyncedAlertness = 0f;
    private float alertnessSyncTimer = 0f;

    // [최적화/추가 이유] 가비지 컬렉터(GC) 방지를 위한 NonAlloc용 캐시 배열
    private Collider[] doorHitColliders = new Collider[5];

    // [기믹 델리게이트] 외부 기믹 스크립트들이 멈춤 여부를 판별해주는 창구
    public delegate bool GimmickPauseCheck();
    [Header("--- Gimmick Events ---")]
    public GimmickPauseCheck OnCheckGimmickPause;
    private bool wasStoppedBeforeFreeze;

    private void Awake()
    {
        stateMachine = new MonsterStateMachine();

        // 1. 공통 상태 등록
        states = new Dictionary<MonsterStateType, IState>
        {
            { MonsterStateType.Patrol, new PatrolState(this) },
            { MonsterStateType.InteractDoor, new InteractDoorState(this) },
            { MonsterStateType.Idle, new PatrolState(this) },
            { MonsterStateType.Dead, new DeadState(this) }
        };

        // 2. 몬스터 타입별 전용 상태 등록
        if (monsterData != null && monsterData.ceilingAttachChance > 0f)
        {
            // [올무벼룩 전용] 공격 상태 대신 특수 상태들만 넣음
            states.Add(MonsterStateType.CeilingWait, new CeilingWaitState(this));
            states.Add(MonsterStateType.Attached, new AttachedState(this));
            states.Add(MonsterStateType.Flee, new FleeState(this));
        }
        else
        {
            // [일반 몬스터용] 일반 공격 상태를 여기서 추가
            states.Add(MonsterStateType.Attack, new AttackState(this));
            states.Add(MonsterStateType.Detect, new DetectState(this));
            states.Add(MonsterStateType.Chase, new ChaseState(this));
            states.Add(MonsterStateType.Search, new SearchState(this));
            states.Add(MonsterStateType.Stunned, new StunnedState(this));
        }
    }

    public override void OnNetworkSpawn()
    {
        navAgent = GetComponent<NavMeshAgent>();
        scanner = GetComponent<EnvironmentScanner>();
        animHandler = GetComponentInChildren<MonsterAnimation>();
        if (animHandler != null) _animator = animHandler.GetComponentInChildren<Animator>();

        scanner.Init(this, monsterData);
        waypointManager = Object.FindAnyObjectByType<WaypointManager>();

        CurrentStateNet.OnValueChanged += OnStateChangedCallback;
        IsFrozenNet.OnValueChanged += OnFrozenNetworkChanged;

        if (!IsServer)
        {
            navAgent.enabled = false;
        }

        ResetMonsterState();
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
        if (CurrentStateNet.Value == MonsterStateType.Dead) return;

        // [서버 전용 로직] 기믹 체크 및 스턴 관리
        if (IsServer)
        {
            HandleGimmickAndFrozenLogic();
            SyncAlertnessOptimized();
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
    /// 창고에서 다시 꺼내질 때 체력, 타겟, 상태를 새것처럼 초기화합니다.
    /// </summary>
    private void ResetMonsterState()
    {
        if (IsServer)
        {
            CurrentHealth.Value = monsterData.maxHealth;
            ServerAlertness = 0f;
            Alertness.Value = 0f;
            IsFrozenNet.Value = false;
            TargetDoor = null;

            // 콜라이더와 에이전트 복구 (Dead 상태에서 꺼졌을 수 있으므로)
            if (TryGetComponent<Collider>(out var col)) col.enabled = true;
            EnableAgentSafely(); // (이전에 알려드린 안전한 에이전트 활성화 함수)

            // 무조건 순찰 상태로 깔끔하게 시작
            ChangeState(MonsterStateType.Patrol);
        }
        else
        {
            ApplyStateLocal(CurrentStateNet.Value);
        }
    }

    /// <summary>
    /// 에이전트를 켜기 전에 현재 위치가 NavMesh 위인지 확인하고, 
    /// 공중이나 격벽이라면 가장 가까운 바닥으로 위치를 보정합니다.
    /// </summary>
    public void EnableAgentSafely()
    {
        if (navAgent == null) return;

        // 반경 3m 내에 있는 가장 가까운 NavMesh 바닥을 찾습니다.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            // 찾은 안전한 바닥으로 몬스터를 살짝 이동시킵니다.
            transform.position = hit.position;
            navAgent.enabled = true; // 이제 안전하게 에이전트를 켭니다.
        }
        else
        {
            // 주변 3m 안에 바닥이 아예 없다면 경고를 띄우고 에이전트를 켜지 않습니다. (에러 방지)
            Debug.LogWarning($"<color=orange>[MonsterController]</color> {gameObject.name} 주변에 NavMesh가 없습니다! 에이전트를 켤 수 없습니다.");

            // 필요하다면 여기서 일정 시간 뒤에 다시 시도하는 코루틴을 부르거나, 
            // 기본 스폰 지점으로 강제 텔레포트 시킬 수 있습니다.
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
                if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                {
                    wasStoppedBeforeFreeze = navAgent.isStopped;
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

    /// <summary>
    /// 특정 플레이어에게 시야 차단(또는 해제) UI를 켜라고 지시
    /// </summary>
    [Rpc(SendTo.SpecifiedInParams)]
    public void TriggerSnareBlindRpc(bool isSnared, RpcParams rpcParams = default)
    {
        if (PlayerUIManager.LocalInstance != null)
        {
            if (isSnared)
            {
                PlayerUIManager.LocalInstance.SetBlindScreen(true);
            }
            else
            {
                PlayerUIManager.LocalInstance.SetBlindScreen(false);
            }
        }
    }

    public float ServerAlertness
    {
        get => serverAlertness;
        set => serverAlertness = Mathf.Clamp01(value);
    }

    private void SyncAlertnessOptimized()
    {
        alertnessSyncTimer += Time.deltaTime;

        float diff = Mathf.Abs(serverAlertness - lastSyncedAlertness);

        // MonsterData에 옮겨둔 최적화 세팅값을 참조합니다!
        if (diff >= monsterData.alertnessThreshold || alertnessSyncTimer >= monsterData.alertnessSyncInterval)
        {
            Alertness.Value = serverAlertness;
            lastSyncedAlertness = serverAlertness;
            alertnessSyncTimer = 0f;
        }
    }

    /// <summary>
    /// 외부(섬광탄 등)에서 몬스터를 스턴시킬 때 호출하는 함수
    /// </summary>
    public void ApplyStun(float baseDuration)
    {
        // 서버만 처리하며, 죽은 상태거나 이미 붙어있는 상태(올무벼룩)면 무시
        if (!IsServer || CurrentStateNet.Value == MonsterStateType.Dead || CurrentStateNet.Value == MonsterStateType.Attached)
            return;

        // SO에 있는 '스턴 내성(Multiplier)'을 곱해서 최종 스턴 시간을 계산!
        float finalDuration = baseDuration * (monsterData != null ? monsterData.stunDurationMultiplier : 1.0f);

        // 스턴 면역(0 이하)이면 스턴에 걸리지 않음
        if (finalDuration <= 0f) return;

        CurrentStunDuration = finalDuration;
        ChangeState(MonsterStateType.Stunned);

        Debug.Log($"<color=cyan>[스턴]</color> {gameObject.name}이(가) {finalDuration}초 동안 기절합니다!");
    }

    // [테스트용] 
    [ContextMenu("Test Damage (10)")]
    public void TestDamage()
    {
        if (IsServer) TakeDamage(50f);
    }
}