using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 상태(FSM), 체력, 네트워크 동기화, 어그로 및 특수 기믹을 총괄하는 핵심 컨트롤러입니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnvironmentScanner))]
public class MonsterController : NetworkBehaviour
{
    // =========================================================
    // 1. 변수 선언부 (Variables)
    // =========================================================

    [Header("--- Monster Configuration ---")]
    [Tooltip("몬스터의 기본 스탯 및 설정 데이터 (ScriptableObject)")]
    public MonsterData monsterData;

    [Header("--- Components & References ---")]
    [Tooltip("주변 환경 감지 시스템 (시각/청각)")]
    public EnvironmentScanner scanner;
    [Tooltip("유니티 네비게이션 에이전트 (길찾기 및 이동 담당)")]
    public NavMeshAgent navAgent;
    [Tooltip("애니메이션 제어 핸들러 (시각적 부드러움 담당)")]
    public MonsterAnimation animHandler;
    [Tooltip("순찰 경로 매니저 (맵에 배치된 Waypoint 리스트)")]
    public WaypointManager waypointManager;

    [Header("--- Network Variables (Synced) ---")]
    [Tooltip("서버에서 관리하며 모든 클라이언트에게 실시간으로 공유되는 체력")]
    public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Tooltip("현재 몬스터의 행동 상태 (이 값이 바뀌면 모든 클라이언트에서 애니메이션/로직이 동기화됨)")]
    public NetworkVariable<MonsterStateType> CurrentStateNet = new NetworkVariable<MonsterStateType>(
        MonsterStateType.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Tooltip("플레이어 감지 경계도 (0~1)")]
    public NetworkVariable<float> Alertness = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Tooltip("현재 빙결/스턴(코일헤드 기믹 등) 상태 여부")]
    public NetworkVariable<bool> IsFrozenNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("--- Gimmick Events ---")]
    [Tooltip("코일헤드 등 외부 기믹 스크립트들이 멈춤 여부를 판별해주는 창구 (GC 발생 없는 List 방식)")]
    public List<Func<bool>> gimmickPauseChecks = new List<Func<bool>>();

    [Header("--- Logic State (Local) ---")]
    [Tooltip("직전 상태 (상태 전환 로직 처리용, 클라이언트/서버 각각 독립적 보관)")]
    public MonsterStateType PreviousState;

    // [프로퍼티] 외부 스크립트에서 읽기만 가능하고, 수정은 내부에서만 진행하는 변수들
    public DoorController TargetDoor { get; set; }
    public float CurrentStunDuration { get; private set; } = 0f;
    public float ServerAlertness
    {
        get => _serverAlertness;
        set => _serverAlertness = Mathf.Clamp01(value);
    }

    // [프라이빗 변수] 컨트롤러 내부에서만 은밀하게 사용하는 변수들 (_ 접두사 사용)
    private MonsterStateMachine _stateMachine;
    private Dictionary<MonsterStateType, IState> _states;
    private Animator _animator;

    private float _serverAlertness = 0f;
    private float _lastSyncedAlertness = 0f;
    private float _alertnessSyncTimer = 0f;
    private bool _wasStoppedBeforeFreeze;

    // 가비지 컬렉터(GC) 스파이크 방지를 위한 NonAlloc 전용 캐시 배열
    private Collider[] _doorHitColliders = new Collider[5];


    // =========================================================
    // 2. 초기화 함수 (Awake / Start / OnNetworkSpawn)
    // =========================================================

    private void Awake()
    {
        _stateMachine = new MonsterStateMachine();

        // 1. 모든 몬스터 공통 상태 등록
        _states = new Dictionary<MonsterStateType, IState>
        {
            { MonsterStateType.Patrol, new PatrolState(this) },
            { MonsterStateType.InteractDoor, new InteractDoorState(this) },
            { MonsterStateType.Idle, new PatrolState(this) }, // Idle은 Patrol 로직을 공유
            { MonsterStateType.Dead, new DeadState(this) }
        };

        // 2. 몬스터 타입(데이터)별 전용 기믹 상태 등록
        if (monsterData != null && monsterData.ceilingAttachChance > 0f)
        {
            // [올무벼룩 전용] 천장 대기, 달라붙기, 도망치기 상태
            _states.Add(MonsterStateType.CeilingWait, new CeilingWaitState(this));
            _states.Add(MonsterStateType.Attached, new AttachedState(this));
            _states.Add(MonsterStateType.Flee, new FleeState(this));
        }
        else
        {
            // [일반 몬스터용] 수색, 추격, 공격, 스턴 상태 등
            _states.Add(MonsterStateType.Attack, new AttackState(this));
            _states.Add(MonsterStateType.Detect, new DetectState(this));
            _states.Add(MonsterStateType.Chase, new ChaseState(this));
            _states.Add(MonsterStateType.Search, new SearchState(this));
            _states.Add(MonsterStateType.Stunned, new StunnedState(this));
            _states.Add(MonsterStateType.Investigate, new InvestigateState(this));
        }
    }

    public override void OnNetworkSpawn()
    {
        // 핵심 컴포넌트 자동 캐싱
        navAgent = GetComponent<NavMeshAgent>();
        scanner = GetComponent<EnvironmentScanner>();
        animHandler = GetComponentInChildren<MonsterAnimation>();
        if (animHandler != null) _animator = animHandler.GetComponentInChildren<Animator>();
        waypointManager = FindAnyObjectByType<WaypointManager>();

        scanner.Init(this, monsterData);

        // 네트워크 변수 콜백 구독 (값이 바뀔 때마다 함수 실행)
        CurrentStateNet.OnValueChanged += OnStateChangedCallback;
        IsFrozenNet.OnValueChanged += OnFrozenNetworkChanged;

        // 클라이언트는 길찾기 연산을 하지 않으므로 에이전트 오프
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

        // 메모리 누수 방지를 위한 구독 해제
        CurrentStateNet.OnValueChanged -= OnStateChangedCallback;
        IsFrozenNet.OnValueChanged -= OnFrozenNetworkChanged;

        base.OnNetworkDespawn();
    }


    // =========================================================
    // 3. 유니티 루프 (Update / FixedUpdate)
    // =========================================================

    private void Update()
    {
        // 이미 죽었으면 모든 로직 정지
        if (CurrentStateNet.Value == MonsterStateType.Dead) return;

        // [서버 전용 로직] 기믹 체크 및 어그로(경계도) 최적화 동기화
        if (IsServer)
        {
            HandleGimmickAndFrozenLogic();
            SyncAlertnessOptimized();
        }

        // 얼어붙은(코일헤드 기믹 등) 상태라면 AI 사고(FSM) 회로 차단
        if (IsFrozenNet.Value) return;

        // 몬스터의 뇌(상태 머신) 업데이트
        _stateMachine?.Update();

        // 시각적 부드러움을 위한 애니메이션 속도 동기화
        if (navAgent != null && animHandler != null)
        {
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
        _stateMachine?.FixedUpdate();
    }


    // =========================================================
    // 4. 퍼블릭 함수 (Public Methods : 외부에서 부르는 창구)
    // =========================================================

    /// <summary>
    /// 서버에서 몬스터의 상태를 변경하고 모든 클라이언트에게 전파합니다.
    /// </summary>
    public void ChangeState(MonsterStateType newState)
    {
        if (!IsServer || CurrentStateNet.Value == newState) return;

        // 값을 변경하면 OnStateChangedCallback이 모든 유저에게 자동 호출됨
        CurrentStateNet.Value = newState;
    }

    /// <summary>
    /// 외부(섬광탄 등)에서 몬스터를 스턴시킬 때 호출하는 창구입니다.
    /// </summary>
    public void ApplyStun(float baseDuration)
    {
        if (!IsServer || CurrentStateNet.Value == MonsterStateType.Dead || CurrentStateNet.Value == MonsterStateType.Attached)
            return;

        float finalDuration = baseDuration * (monsterData != null ? monsterData.stunDurationMultiplier : 1.0f);
        if (finalDuration <= 0f) return;

        CurrentStunDuration = finalDuration;
        ChangeState(MonsterStateType.Stunned);

        Debug.Log($"<color=cyan>[스턴]</color> {gameObject.name}이(가) {finalDuration}초 동안 기절합니다!");
    }

    /// <summary>
    /// 플레이어의 무기 등에서 몬스터에게 데미지를 줄 때 호출합니다.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (!IsServer || CurrentStateNet.Value == MonsterStateType.Dead) return;

        CurrentHealth.Value -= damage;
        Debug.Log($"<color=red>[몬스터 피격]</color> {gameObject.name} 남은 체력: {CurrentHealth.Value}");

        if (CurrentHealth.Value <= 0)
        {
            CurrentHealth.Value = 0;
            ChangeState(MonsterStateType.Dead);
            return;
        }

        // 머리에 붙어있는 올무벼룩이 맞았을 경우 즉시 도망 상태로 전환
        if (CurrentStateNet.Value == MonsterStateType.Attached)
        {
            ChangeState(MonsterStateType.Flee);
        }
    }

    /// <summary>
    /// 특정 게임오브젝트가 안전 구역(SafeZone) 레이어인지 판별합니다.
    /// </summary>
    public bool IsInSafeZone(GameObject obj)
    {
        return (1 << obj.layer & LayerMask.GetMask("SafeZone")) != 0;
    }

    /// <summary>
    /// 애니메이션 이벤트에서 호출되어 실제 데미지를 입히는 로직을 실행합니다.
    /// </summary>
    public void ExecuteAttackDamage()
    {
        if (_stateMachine.CurrentState is AttackState attackState)
        {
            attackState.ApplyDamageToTarget();
        }
    }

    /// <summary>
    /// 전방의 문을 탐색하고 발견 시 확률에 따라 상호작용(문 열기) 상태로 전환합니다.
    /// </summary>
    /// <param name="openChance">문을 열 확률 (0.0 ~ 1.0). 기본값은 1.0(100%)</param>
    public bool CheckAndHandleDoor(float openChance = 1.0f)
    {
        if (!IsServer) return false;

        Vector3 checkPos = transform.position + (Vector3.up * 1.0f);
        int doorLayerMask = 1 << LayerMask.NameToLayer("Door");

        int hitCount = Physics.OverlapSphereNonAlloc(checkPos, 1.2f, _doorHitColliders, doorLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _doorHitColliders[i];
            DoorController door = hit.GetComponentInParent<DoorController>();

            if (door != null && !door.isOpen)
            {
                Vector3 dirToDoor = (hit.bounds.center - transform.position).normalized;

                // 문이 전방 180도 이내에 있다면
                if (Vector3.Dot(transform.forward, dirToDoor) > -0.2f)
                {
                    // 확률 굴림 (openChance가 0.3이면 30% 확률로만 성공)
                    if (UnityEngine.Random.value <= openChance)
                    {
                        TargetDoor = door;
                        ChangeState(MonsterStateType.InteractDoor);
                    }

                    // 확률에 실패해서 문을 안 열었더라도, "문이 앞을 막고 있다(true)"는 사실은 반환합니다.
                    // 그래야 PatrolState에서 이 사실을 알고 다른 길로 돌아갑니다.
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 올무벼룩이 특정 플레이어에게 시야 차단 UI를 켜거나 끄도록 지시하는 서버 RPC입니다.
    /// </summary>
    [Rpc(SendTo.SpecifiedInParams)]
    public void TriggerSnareBlindRpc(bool isSnared, RpcParams rpcParams = default)
    {
        if (PlayerUIManager.LocalInstance != null)
        {
            PlayerUIManager.LocalInstance.SetBlindScreen(isSnared);
        }
    }

    [ContextMenu("Test Damage (50)")]
    public void TestDamage()
    {
        if (IsServer) TakeDamage(50f);
    }


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 (Private Methods : 내부 연산용)
    // =========================================================

    /// <summary>
    /// 상태(CurrentStateNet) 값이 변경될 때마다 모든 클라이언트에서 자동 실행되는 콜백
    /// </summary>
    private void OnStateChangedCallback(MonsterStateType previousValue, MonsterStateType newValue)
    {
        PreviousState = previousValue;
        ApplyStateLocal(newValue);

        // 공격 모션 도중 다른 상태로 강제 전환되면 공격 애니메이션 취소
        if (previousValue == MonsterStateType.Attack && newValue != MonsterStateType.Attack)
        {
            if (animHandler != null) animHandler.CancelAttack();
        }

        Debug.Log($"[Sync] {gameObject.name} State: {previousValue} -> {newValue}");
    }

    /// <summary>
    /// 실제 딕셔너리에서 상태 클래스를 찾아 상태 머신에 밀어넣습니다.
    /// </summary>
    private void ApplyStateLocal(MonsterStateType newState)
    {
        if (_states.TryGetValue(newState, out IState stateInstance))
        {
            _stateMachine.ChangeState(stateInstance);
        }
    }

    /// <summary>
    /// 얼어붙음 상태가 변경될 때 애니메이션 속도를 조절하여 완전히 굳어버리게 연출합니다.
    /// </summary>
    private void OnFrozenNetworkChanged(bool previous, bool current)
    {
        if (_animator != null)
        {
            _animator.speed = current ? 0f : 1f;
        }
    }

    /// <summary>
    /// 몬스터가 처음 스폰되거나 풀링 창고에서 다시 꺼내질 때 새것처럼 수치를 초기화합니다.
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

            if (TryGetComponent<Collider>(out var col)) col.enabled = true;
            EnableAgentSafely();

            ChangeState(MonsterStateType.Patrol);
        }
        else
        {
            ApplyStateLocal(CurrentStateNet.Value);
        }
    }

    /// <summary>
    /// 공중이나 허공에 스폰되었을 경우, 반경 3m 내의 바닥을 찾아 안전하게 에이전트를 켭니다.
    /// </summary>
    public void EnableAgentSafely()
    {
        if (navAgent == null) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            navAgent.enabled = true;
        }
        else
        {
            Debug.LogWarning($"<color=orange>[MonsterController]</color> {gameObject.name} 주변에 NavMesh가 없습니다! 에이전트를 켤 수 없습니다.");
        }
    }

    /// <summary>
    /// 리스트에 등록된 기믹 조건(예: 누군가 쳐다봄)들을 검사하여 빙결 여부를 세팅합니다.
    /// </summary>
    private void HandleGimmickAndFrozenLogic()
    {
        bool shouldPause = false;

        // 가비지 발생 없이 안전하게 리스트 순회
        for (int i = 0; i < gimmickPauseChecks.Count; i++)
        {
            if (gimmickPauseChecks[i].Invoke())
            {
                shouldPause = true;
                break;
            }
        }

        // 상태가 변했을 때만 네트워크 값 및 네비게이션 제어
        if (IsFrozenNet.Value != shouldPause)
        {
            IsFrozenNet.Value = shouldPause;

            if (shouldPause)
            {
                if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                {
                    _wasStoppedBeforeFreeze = navAgent.isStopped;
                    navAgent.isStopped = true;
                    navAgent.velocity = Vector3.zero;
                }
            }
            else
            {
                if (navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = _wasStoppedBeforeFreeze;
                }

                // 빙결이 풀렸을 때 타겟이 이미 사거리를 벗어났는지 재평가
                if (CurrentStateNet.Value == MonsterStateType.Attack)
                {
                    Transform target = scanner.CurrentTarget;
                    if (target != null)
                    {
                        float sqrDist = (target.position - transform.position).sqrMagnitude;
                        float hitThreshold = monsterData.attackRange + 0.5f;

                        if (sqrDist > hitThreshold * hitThreshold)
                        {
                            ChangeState(MonsterStateType.Chase);
                        }
                    }
                    else
                    {
                        ChangeState(MonsterStateType.Search);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 네트워크 과부하를 막기 위해, 경계도(Alertness) 값이 임계치 이상 변하거나 일정 시간이 지났을 때만 동기화합니다.
    /// </summary>
    private void SyncAlertnessOptimized()
    {
        _alertnessSyncTimer += Time.deltaTime;

        float diff = Mathf.Abs(_serverAlertness - _lastSyncedAlertness);

        if (diff >= monsterData.alertnessThreshold || _alertnessSyncTimer >= monsterData.alertnessSyncInterval)
        {
            Alertness.Value = _serverAlertness;
            _lastSyncedAlertness = _serverAlertness;
            _alertnessSyncTimer = 0f;
        }
    }
}