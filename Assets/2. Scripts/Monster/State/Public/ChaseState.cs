using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터가 플레이어를 발견하고 맹렬하게 쫓아가는 추격 상태입니다.
/// </summary>
public class ChaseState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private Vector3 _lastTargetPos;
    private float _stuckTimer;
    private float _pathUpdateSqrThreshold;

    // =========================================================
    // 2. 초기화 함수
    // =========================================================

    public ChaseState(MonsterController owner) : base(owner)
    {
        // 추격 상태는 비교적 빠른 상황 판단이 필요하므로 AI 틱 주기를 기본값(0.2초)으로 사용합니다.
    }

    public override void Enter()
    {
        base.Enter();

        // 1. 에이전트 이동 설정
        owner.navAgent.isStopped = false;
        owner.navAgent.speed = data.chaseSpeed;

        // 플레이어를 항상 정면으로 바라보게 만들기 위해 네비게이션 자동 회전을 끕니다.
        owner.navAgent.updateRotation = false;

        // 2. 내부 변수 초기화
        _lastTargetPos = Vector3.positiveInfinity;
        _stuckTimer = 0f;

        // 경로 재탐색 기준 거리(제곱값) 캐싱
        _pathUpdateSqrThreshold = data.pathUpdateThreshold * data.pathUpdateThreshold;
    }

    public override void Exit()
    {
        base.Exit();
        // 추격이 끝나면 다시 에이전트가 이동 방향을 바라보도록 자동 회전을 켜줍니다.
        owner.navAgent.updateRotation = true;
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 시각적으로 부드러워야 하는 회전(Rotation)만 담당합니다. 
    /// </summary>
    public override void Update()
    {
        base.Update();

        if (!owner.IsServer) return;

        Transform target = owner.scanner.CurrentTarget;

        // 타겟이 시야에 확실히 보일 때만 매 프레임 부드럽게 유도 회전을 수행합니다.
        if (target != null && !owner.IsInSafeZone(target.gameObject))
        {
            HandleRotation(target);
        }
    }

    /// <summary>
    /// 0.2초마다 실행: 무거운 길찾기, 문 감지, 사거리 판정 등 AI 두뇌 역할을 담당합니다.
    /// </summary>
    protected override void OnTick()
    {
        // 1. 감각 시스템 업데이트
        owner.scanner.Tick();

        if (!owner.IsServer || owner.CurrentStateNet.Value != MonsterStateType.Chase) return;

        // 2. 전방에 닫힌 문이 있는지 확인
        if (owner.CheckAndHandleDoor()) return;

        Transform target = owner.scanner.CurrentTarget;

        // 3. 타겟을 놓쳤거나 안전지대에 들어갔을 때 (마지막 목격 위치로 이동)
        if (target == null || owner.IsInSafeZone(target.gameObject))
        {
            HandleTargetLost();
            return;
        }

        // 4. 타겟이 보일 때 (실시간 추격 로직)
        _stuckTimer = 0f; // 타겟이 정상적으로 보이면 끼임(Stuck) 타이머 초기화

        CheckAttackDistance(target);
        UpdatePath(target);
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 타겟을 시야에서 놓쳤을 때 마지막 목격 위치를 향해 달려가는 로직입니다.
    /// </summary>
    private void HandleTargetLost()
    {
        // 타겟이 없으므로, 몬스터가 이동하는 방향을 자연스럽게 바라보도록 자동 회전 복구
        owner.navAgent.updateRotation = true;

        // 목적지가 바뀌었을 때만 SetDestination을 호출하여 길찾기 연산을 아낍니다.
        if (Vector3.Distance(owner.navAgent.destination, owner.scanner.LastSeenPosition) > 0.5f)
        {
            owner.navAgent.SetDestination(owner.scanner.LastSeenPosition);
        }

        if (!owner.navAgent.pathPending)
        {
            // [끼임 체크] 목적지까지 거리가 남았는데 속도가 거의 0이라면 (문이나 벽에 막힘)
            if (owner.navAgent.velocity.sqrMagnitude < 0.2f && owner.navAgent.remainingDistance > 0.5f)
            {
                _stuckTimer += currentTickInterval; // OnTick 주기에 맞춰 타이머 증가

                if (_stuckTimer > 0.5f) // 0.5초간 막히면 즉시 문 체크
                {
                    if (owner.CheckAndHandleDoor()) return;

                    if (_stuckTimer > 2.0f) // 2초 넘게 방법이 없으면 포기하고 수색 전환
                    {
                        owner.ChangeState(MonsterStateType.Search);
                        return;
                    }
                }
            }
            else
            {
                _stuckTimer = 0f;
            }

            // [도착 판정] 마지막 목격 지점에 도착했는데도 타겟이 없으면 수색(두리번) 모드로 전환
            if (owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 1.0f)
            {
                owner.ChangeState(MonsterStateType.Search);
            }
        }
    }

    /// <summary>
    /// 타겟을 향해 부드럽게 몸을 돌립니다 (매 프레임 Update에서 호출됨)
    /// </summary>
    private void HandleRotation(Transform target)
    {
        Vector3 dir = (target.position - owner.transform.position).normalized;
        dir.y = 0; // 수직(위아래) 회전 방지

        if (dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            owner.transform.rotation = Quaternion.RotateTowards(
                owner.transform.rotation,
                targetRotation,
                Time.deltaTime * 500f
            );
        }
    }

    /// <summary>
    /// 공격 사거리 내에 타겟이 들어왔는지 판정합니다.
    /// </summary>
    private void CheckAttackDistance(Transform target)
    {
        // 올무벼룩(천장 대기형)은 일반 공격을 하지 않으므로 패스
        if (data.ceilingAttachChance > 0f) return;

        Vector3 offset = target.position - owner.transform.position;
        if (offset.sqrMagnitude <= data.attackRange * data.attackRange)
        {
            owner.ChangeState(MonsterStateType.Attack);
        }
    }

    /// <summary>
    /// 타겟이 일정 거리 이상 움직였을 때만 길을 새로 찾습니다 
    /// </summary>
    private void UpdatePath(Transform target)
    {
        Vector3 moveDelta = target.position - _lastTargetPos;

        // 타겟이 기준치(pathUpdateThreshold) 이상 움직여야만 목적지를 갱신
        if (moveDelta.sqrMagnitude > _pathUpdateSqrThreshold)
        {
            owner.navAgent.SetDestination(target.position);
            _lastTargetPos = target.position;
        }

        CheckStuck();
    }

    /// <summary>
    /// 경로가 단절되거나 지형지물에 완전히 꼈을 경우를 검사합니다.
    /// </summary>
    private void CheckStuck()
    {
        if (!owner.navAgent.pathPending &&
            (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial ||
             owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid))
        {
            _stuckTimer += currentTickInterval;

            // 1.5초 이상 경로를 찾지 못하면 추격을 포기하고 수색으로 전환
            if (_stuckTimer > 1.5f)
            {
                owner.ChangeState(MonsterStateType.Search);
            }
        }
    }
}