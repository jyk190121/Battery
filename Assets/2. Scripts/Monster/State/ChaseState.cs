using UnityEngine;
using UnityEngine.AI;

public class ChaseState : MonsterBaseState
{
    private Vector3 lastTargetPos;
    private float stuckTimer;

    // 미리 계산해둘 제곱값 캐싱용 변수
    private float pathUpdateSqrThreshold;

    public ChaseState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();
        owner.navAgent.isStopped = false;
        owner.navAgent.speed = data.chaseSpeed;

        // 몸은 이동 경로를 따라가되, 고개(방향)는 수동으로 제어하기 위해 자동 회전을 끕니다.
        owner.navAgent.updateRotation = false;

        lastTargetPos = Vector3.positiveInfinity;
        stuckTimer = 0f;
        owner.animHandler.SetSpeed(2f); 

        pathUpdateSqrThreshold = data.pathUpdateThreshold * data.pathUpdateThreshold;
    }

    protected override void OnTick()
    {
        owner.scanner.Tick();

        if (owner.CurrentStateNet.Value == MonsterStateType.Chase)
        {
            owner.CheckAndHandleDoor();
        }
    }

    public override void Update()
    {
        base.Update();

        // 1. 문 감지 (가장 최상단에서 우선 실행)
        if (owner.CheckAndHandleDoor()) return;

        Transform target = owner.scanner.CurrentTarget;

        // 2. 타겟을 놓쳤거나 안전구역에 들어갔을 때
        if (target == null || owner.IsInSafeZone(target.gameObject))
        {
            owner.navAgent.updateRotation = true;

            // [수정 핵심: SetDestination 스팸 방지]
            // 현재 설정된 목적지가 LastSeenPosition과 다를 때만 '딱 한 번' 경로를 계산합니다.
            if (Vector3.Distance(owner.navAgent.destination, owner.scanner.LastSeenPosition) > 0.1f)
            {
                owner.navAgent.SetDestination(owner.scanner.LastSeenPosition);
            }

            // 경로 계산이 완료된 상태에서만 거리 및 끼임 체크 실행
            if (!owner.navAgent.pathPending)
            {
                // [추가] 물리적 정지 체크 (문에 가로막혀 속도가 안 날 때)
                if (owner.navAgent.velocity.sqrMagnitude < 0.2f && owner.navAgent.remainingDistance > 0.5f)
                {
                    stuckTimer += Time.deltaTime;
                    // 0.3초만 비비고 있어도 바로 문 감지 실행 (더 예민하게 대응)
                    if (stuckTimer > 0.3f)
                    {
                        if (owner.CheckAndHandleDoor()) return;

                        // 문도 없고 2초 이상 끼어있으면 수색으로 포기
                        if (stuckTimer > 2.0f)
                        {
                            owner.ChangeState(MonsterStateType.Search);
                            return;
                        }
                    }
                }
                else
                {
                    stuckTimer = 0f;
                }

                // 정상 도착 판정
                if (owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 1.0f)
                {
                    owner.ChangeState(MonsterStateType.Search);
                    return;
                }
            }
            return;
        }

        // --- 여기서부터는 플레이어가 시야에 확실히 보일 때의 로직 ---

        // 플레이어를 실시간으로 바라보게
        Vector3 dir = (target.position - owner.transform.position).normalized;
        dir.y = 0; // 하늘이나 땅을 보지 않도록 평면 회전

        if (dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            // Slerp나 RotateTowards를 사용하여 부드럽게 회전
            owner.transform.rotation = Quaternion.RotateTowards(
                owner.transform.rotation,
                targetRotation,
                Time.deltaTime * 500f // 초당 500도 회전
            );
        }

        // 3. 공격 사거리 체크
        Vector3 offset = target.position - owner.transform.position;
        if (offset.sqrMagnitude <= data.attackRange * data.attackRange)
        {
            owner.ChangeState(MonsterStateType.Attack);
            return;
        }

        // 4. 경로 탐색 갱신 
        Vector3 moveDelta = target.position - lastTargetPos;

        // 플레이어가 일정 거리 이상 움직였을 때만 NavMesh 목적지를 갱신
        if (moveDelta.sqrMagnitude > pathUpdateSqrThreshold)
        {
            // 몸(물리)은 실제 플레이어 위치로 이동 명령
            owner.navAgent.SetDestination(target.position);
            lastTargetPos = target.position;
        }

        // 5. 끼임 체크
        CheckStuck();
    }

    private void CheckStuck()
    {
        if (!owner.navAgent.pathPending &&
            (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial ||
             owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid))
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.5f)
            {
                Debug.LogWarning("추격 중 경로 끊김. 수색 모드로 전환.");
                owner.ChangeState(MonsterStateType.Search);
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    public override void Exit()
    {
        // 상태를 나갈 때 자동 회전을 다시 켜줍니다. 
        owner.navAgent.updateRotation = true;
    }
}