using System;
using System.Collections;
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
    // 1. 변수 선언부
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

    [Header("--- Map Zone ---")]
    [Tooltip("이 몬스터가 활동할 구역 (일반몹: School, 고스트: SpiritualWorld)")]
    public MapZone currentZone = MapZone.School;

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
    // 2. 초기화 함수 
    // =========================================================

    private void Awake()
    {
        _stateMachine = new MonsterStateMachine();

        // 1. 모든 몬스터 공통 상태 등록 (순찰, 대기, 사망)
        _states = new Dictionary<MonsterStateType, IState>
        {
            { MonsterStateType.Patrol, new PatrolState(this) },
            { MonsterStateType.Idle, new PatrolState(this) },   // Idle은 Patrol 로직을 공유
            { MonsterStateType.Dead, new DeadState(this) }
        };

        // 2. 몬스터 타입(데이터)별 전용 기믹 및 추가 상태 등록
        if (monsterData != null)
        {
            // [인형 전용]
            if (monsterData.type == MonsterType.Special)
            {
                _states.Add(MonsterStateType.Stalk, new StalkState(this));
                _states.Add(MonsterStateType.Scream, new ScreamState(this));
            }
            // [올무벼룩 전용]
            else if (monsterData.type == MonsterType.Ambush)
            {
                _states.Add(MonsterStateType.CeilingWait, new CeilingWaitState(this));
                _states.Add(MonsterStateType.Attached, new AttachedState(this));
                _states.Add(MonsterStateType.Flee, new FleeState(this));
            }
            // [고스트 전용]
            else if (monsterData.type == MonsterType.Ghost)
            {
                _states.Add(MonsterStateType.Attack, new AttackState(this));
                _states.Add(MonsterStateType.Detect, new DetectState(this));
                _states.Add(MonsterStateType.Chase, new ChaseState(this));
                _states.Add(MonsterStateType.Search, new SearchState(this));
                _states.Add(MonsterStateType.Investigate, new InvestigateState(this));
            }
            // [일반 몬스터] 
            else
            {
                _states.Add(MonsterStateType.Attack, new AttackState(this));
                _states.Add(MonsterStateType.Detect, new DetectState(this));
                _states.Add(MonsterStateType.Chase, new ChaseState(this));
                _states.Add(MonsterStateType.Search, new SearchState(this));
                _states.Add(MonsterStateType.Investigate, new InvestigateState(this));
                _states.Add(MonsterStateType.Stunned, new StunnedState(this));
                _states.Add(MonsterStateType.InteractDoor, new InteractDoorState(this));
            }
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

        // 특정 타입(Ambush)일 때만 카메라 등록 진행
        if (monsterData != null && monsterData.type == MonsterType.Ambush)
        {
            RegisterAmbushCamera();
        }

        // [버그 픽스] 그냥 함수 호출이 아니라 안전한 딜레이 스폰 코루틴을 돌립니다.
        StartCoroutine(ResetMonsterStateRoutine());
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
    // 3. 유니티 루프
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
    // 4. 퍼블릭 함수
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

        if (monsterData.type == MonsterType.Ghost || monsterData.type == MonsterType.Special) return;

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

        if (monsterData.type == MonsterType.Ghost || monsterData.type == MonsterType.Special) return;

        CurrentHealth.Value -= damage;
        Debug.Log($"<color=red>[몬스터 피격]</color> {gameObject.name} 남은 체력: {CurrentHealth.Value}");

        PlayHitEffectClientRpc();

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

    [ClientRpc]
    private void PlayHitEffectClientRpc()
    {
        // 1. 애니메이션 재생 (예: Hit 트리거)
        if (_animator != null) _animator.SetTrigger("Hit");
        Debug.Log($"<color=orange>[Client]</color> {gameObject.name} 피격 비주얼 재생");
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
    public bool CheckAndHandleDoor(float openChance = 1.0f)
    {
        if (!IsServer) return false;

        if (!_states.ContainsKey(MonsterStateType.InteractDoor)) return false;

        Vector3 checkPos = transform.position + (Vector3.up * 1.0f);
        int doorLayerMask = 1 << LayerMask.NameToLayer("Door");

        int hitCount = Physics.OverlapSphereNonAlloc(checkPos, 1.2f, _doorHitColliders, doorLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _doorHitColliders[i];
            DoorController door = hit.GetComponentInParent<DoorController>();

            if (door != null && !door.isOpen.Value)
            {
                Vector3 dirToDoor = (hit.bounds.center - transform.position).normalized;

                // 문이 전방 180도 이내에 있다면
                if (Vector3.Dot(transform.forward, dirToDoor) > -0.2f)
                {
                    if (UnityEngine.Random.value <= openChance)
                    {
                        TargetDoor = door;
                        ChangeState(MonsterStateType.InteractDoor);
                    }
                    return true;
                }
            }
        }
        return false;
    }

    [ContextMenu("Test Damage (50)")]
    public void TestDamage()
    {
        if (IsServer) TakeDamage(50f);
    }


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    private void OnStateChangedCallback(MonsterStateType previousValue, MonsterStateType newValue)
    {
        PreviousState = previousValue;
        ApplyStateLocal(newValue);

        if (previousValue == MonsterStateType.Attack && newValue != MonsterStateType.Attack)
        {
            if (animHandler != null) animHandler.CancelAttack();
        }

        Debug.Log($"[Sync] {gameObject.name} State: {previousValue} -> {newValue}");
    }

    private void ApplyStateLocal(MonsterStateType newState)
    {
        if (_states.TryGetValue(newState, out IState stateInstance))
        {
            _stateMachine.ChangeState(stateInstance);
        }
    }

    private void OnFrozenNetworkChanged(bool previous, bool current)
    {
        if (_animator != null)
        {
            _animator.speed = current ? 0f : 1f;
        }
    }

    /// <summary>
    /// 딜레이 스폰 코루틴
    /// 몬스터가 NavMesh 위에 완벽하게 올라간 것을 확인한 후에만 AI를 가동시켜 초기화 에러를 막습니다.
    /// </summary>
    private IEnumerator ResetMonsterStateRoutine()
    {
        if (IsServer)
        {
            CurrentHealth.Value = monsterData.maxHealth;
            ServerAlertness = 0f;
            Alertness.Value = 0f;
            IsFrozenNet.Value = false;
            TargetDoor = null;

            if (TryGetComponent<Collider>(out var col)) col.enabled = true;

            // 1. 강제 바인딩 시도
            EnableAgentSafely();

            // 2. 엔진이 완전히 인식할 때까지 대기 (Race Condition 차단)
            yield return new WaitUntil(() => navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh);

            // 3. 발이 닿은 것을 확인했으므로 당당하게 AI(상태) 시작
            ChangeState(MonsterStateType.Patrol);
        }
        else
        {
            ApplyStateLocal(CurrentStateNet.Value);
        }
    }

    /// <summary>
    /// 공중 스폰 방지 및 강제 바인딩
    /// </summary>
    public void EnableAgentSafely()
    {
        if (navAgent == null) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            navAgent.enabled = true;
            // 일반 position 대입이 아니라 엔진에 바인딩하는 Warp 사용
            navAgent.Warp(hit.position);
        }
        else
        {
            Debug.LogWarning($"<color=orange>[MonsterController]</color> {gameObject.name} 주변에 NavMesh가 없습니다! 에이전트를 켤 수 없습니다.");
        }
    }

    private void HandleGimmickAndFrozenLogic()
    {
        bool shouldPause = false;

        for (int i = 0; i < gimmickPauseChecks.Count; i++)
        {
            if (gimmickPauseChecks[i].Invoke())
            {
                shouldPause = true;
                break;
            }
        }

        if (IsFrozenNet.Value != shouldPause)
        {
            IsFrozenNet.Value = shouldPause;

            if (shouldPause)
            {
                // Freeze 방어벽
                if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                {
                    _wasStoppedBeforeFreeze = navAgent.isStopped;
                    navAgent.isStopped = true;
                    navAgent.velocity = Vector3.zero;
                }
            }
            else
            {
                // Freeze 해제 방어벽
                if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = _wasStoppedBeforeFreeze;
                }

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

    void RegisterAmbushCamera()
    {
        if (CinemachineController.Instance != null)
        {
            var vcam = GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);

            if (vcam != null)
            {
                CinemachineController.Instance.RegisterMonsterCamera(vcam);
            }
            else
            {
                Debug.LogWarning($"[MonsterController] {gameObject.name}는 Ambush 타입이지만 자식에 CinemachineCamera가 없습니다.");
            }
        }
    }
}