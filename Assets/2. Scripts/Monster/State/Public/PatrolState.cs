using UnityEngine;

/// <summary>
/// 몬스터가 맵을 배회하며 타겟을 찾는 기본 상태(순찰)입니다.
/// 목적지에 도착하면 잠시 대기한 후 다음 목적지로 이동하며, 지형에 끼임(Stuck) 현상을 스스로 탈출합니다.
/// </summary>
public class PatrolState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private bool _isWaiting;
    private float _waitTimer;
    private float _currentWaitDuration;
    private float _stuckTimer;
    private float _fleaCheckTimer;
    private float _doorDecisionTimer;

    // =========================================================
    // 2. 초기화 함수
    // =========================================================

    public PatrolState(MonsterController owner) : base(owner)
    {
        // 순찰 상태는 비교적 여유로우므로 기본 틱(0.2초) 주기를 사용합니다.
    }

    public override void Enter()
    {
        base.Enter();

        // 1. 네비게이션 에이전트 활성화 보장 (사망 등 비활성 상태에서 복구될 때 대비)
        if (owner.navAgent != null && !owner.navAgent.enabled)
        {
            owner.navAgent.enabled = true;
        }

        // 2. 이동 속도 및 상태 초기화
        owner.navAgent.speed = data.patrolSpeed;
        owner.navAgent.isStopped = false;

        _isWaiting = false;
        _stuckTimer = 0f; // 이동 시작 시 타이머 초기화
        _fleaCheckTimer = 0f;
        _doorDecisionTimer = 0f;

        // 3. 진입과 동시에 첫 번째 목적지로 이동
        MoveToNextPoint();
    }

    public override void Exit()
    {
        base.Exit();
        // 순찰을 끝내고 다른 상태로 넘어갈 때 필요한 초기화 (현재는 비워둠)
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 목적지 도착 여부 확인 및 대기 타이머를 계산합니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        if (owner.CurrentStateNet.Value != MonsterStateType.Patrol) return;

        // 상태에 따라 도착 확인 또는 대기 로직 분기
        if (!_isWaiting)
        {
            CheckArrival();
        }
        else
        {
            HandleWaiting();
        }
    }

    /// <summary>
    /// 0.2초마다 실행: 시야/청각 감지 및 닫힌 문을 탐색하는 AI 두뇌 역할을 합니다.
    /// </summary>
    protected override void OnTick()
    {
        owner.scanner.Tick();

        if (owner.scanner.CurrentTarget != null)
        {
            if (owner.monsterData.name == "Doll")
            {
                owner.ChangeState(MonsterStateType.Stalk); 
            }
            else
            {
                owner.ChangeState(MonsterStateType.Detect); 
            }
            return;
        }

        if (!_isWaiting)
        {
            // 1. 문 처리 로직 (집요함 버전)
            HandleDoorWithPersistence();

            // 2. 올무벼룩 천장 체크 (이동 중에만 수행)
            if (data.ceilingAttachChance > 0f)
            {
                _fleaCheckTimer += currentTickInterval;

                if (_fleaCheckTimer >= data.fleaCeilingCheckInterval)
                {
                    _fleaCheckTimer = 0f;
                    if (Random.value <= data.ceilingAttachChance)
                    {
                        owner.ChangeState(MonsterStateType.CeilingWait);
                    }
                }
            }
        }
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 문 앞에서 목적지를 쉽게 바꾸지 않고 고민하는 로직
    /// </summary>
    private void HandleDoorWithPersistence()
    {
        if (_doorDecisionTimer > 0f)
        {
            _doorDecisionTimer -= currentTickInterval;
            return;
        }

        if (owner.CheckAndHandleDoor(data.patrolDoorOpenChance))
        {
            if (owner.CurrentStateNet.Value == MonsterStateType.Patrol)
            {
                _doorDecisionTimer = data.doorIgnoreCooldown; // 기본 쿨타임 적용

                if (UnityEngine.Random.value <= 0.5f)
                {
                    Debug.Log("<color=green>[Patrol]</color> 문을 열지 않지만, 수상함을 느끼고 문 앞에서 대기하며 귀를 기울입니다...");

                    StartWaiting(2f, 4f);
                }
                // 나머지 50% 확률로는 쿨하게 포기하고 다른 길로 떠납니다.
                else
                {
                    Debug.Log("<color=green>[Patrol]</color> 문이 막혔고 열지 않기로 했습니다. 미련 없이 다른 길로 돌아갑니다.");
                    MoveToNextPoint();
                }
            }
        }
        else
        {
            if (owner.navAgent.isStopped) owner.navAgent.isStopped = false;
        }
    }

    /// <summary>
    /// 맵에 배치된 웨이포인트 중 하나를 무작위로 골라 이동을 시작합니다.
    /// </summary>
    private void MoveToNextPoint()
    {
        Transform nextPoint = owner.waypointManager?.GetFarWaypoint(owner.transform.position, data.minPatrolDistance);

        if (nextPoint != null)
        {
            Debug.Log($"<color=green>[Patrol]</color> {nextPoint.name} 지점으로 순찰 이동 시작");

            owner.navAgent.SetDestination(nextPoint.position);
            owner.navAgent.isStopped = false;

            _stuckTimer = 0f; // 새 목적지로 출발하므로 끼임 타이머 초기화
        }
    }

    /// <summary>
    /// 목적지 도달 여부 및 지형지물에 끼었는지(Stuck)를 판별합니다.
    /// </summary>
    private void CheckArrival()
    {
        _stuckTimer += Time.deltaTime;

        // [끼임 방지 시스템] 너무 오랫동안 목적지에 도착하지 못했다면 강제 탈출
        if (_stuckTimer >= data.maxPatrolMoveTime)
        {
            Debug.LogWarning($"<color=orange>[Patrol]</color> {owner.gameObject.name}이(가) 지형에 끼었거나 목적지 도달에 실패했습니다. 새로운 경로를 탐색합니다.");
            MoveToNextPoint();
            return;
        }

        // 목적지에 거의 도착했는지 확인 (remainingDistance가 stoppingDistance 이내로 들어올 때)
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance)
        {
            // [기믹: 올무벼룩] 확률적으로 천장 대기 상태로 돌입
            if (data.ceilingAttachChance > 0f && Random.value <= data.ceilingAttachChance)
            {
                owner.ChangeState(MonsterStateType.CeilingWait);
                return;
            }

            // 일반적인 대기 모드 돌입
            StartWaiting(data.minWaitTime, data.maxWaitTime);
        }
    }

    /// <summary>
    /// 목적지에 도착한 후 다음 장소로 가기 전까지 지정된 시간 동안 대기합니다.
    /// </summary>
    private void StartWaiting(float minTime, float maxTime)
    {
        _isWaiting = true;
        _waitTimer = 0f;

        _currentWaitDuration = Random.Range(minTime, maxTime);

        // 관성에 의해 미끄러지는 시각적 버그를 막기 위해 에이전트를 확실히 정지
        owner.navAgent.isStopped = true;
        owner.navAgent.velocity = Vector3.zero;
    }

    /// <summary>
    /// 대기 시간을 계산하고, 시간이 다 되면 다음 목적지를 향해 출발시킵니다.
    /// </summary>
    private void HandleWaiting()
    {
        _waitTimer += Time.deltaTime;

        // 대기 시간이 끝나면
        if (_waitTimer >= _currentWaitDuration)
        {
            _isWaiting = false;
            MoveToNextPoint();
        }
    }
}