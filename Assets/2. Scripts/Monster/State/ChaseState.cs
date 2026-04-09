using UnityEngine;
using UnityEngine.AI;

public class ChaseState : MonsterBaseState
{
    private Vector3 lastTargetPos;
    private float stuckTimer;

    // 미리 계산해둘 제곱값 캐싱용 변수 (하드코딩 제거!)
    private float pathUpdateSqrThreshold;

    public ChaseState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();
        owner.navAgent.isStopped = false;
        owner.navAgent.speed = data.chaseSpeed;

        lastTargetPos = Vector3.positiveInfinity;
        stuckTimer = 0f;
        owner.animHandler.SetSpeed(2f);

        // [핵심] 유저님의 data를 가져오되, 매 프레임 곱셈을 피하기 위해 Enter에서 한 번만 제곱해둡니다.
        pathUpdateSqrThreshold = data.pathUpdateThreshold * data.pathUpdateThreshold;
    }

    protected override void OnTick()
    {
        owner.scanner.Tick();
    }

    public override void Update()
    {
        base.Update();

        // 1. 문 감지
        if (owner.CheckAndHandleDoor()) return;

        Transform target = owner.scanner.CurrentTarget;

        // 2. 타겟을 놓쳤을 때
        if (target == null || owner.IsInSafeZone(target.gameObject))
        {
            owner.navAgent.SetDestination(owner.scanner.LastSeenPosition);

            if (!owner.navAgent.pathPending)
            {
                if (owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 1.0f)
                {
                    owner.ChangeState(MonsterStateType.Search);
                    return;
                }

                if (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial ||
                    owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer > 1.5f)
                    {
                        owner.ChangeState(MonsterStateType.Search);
                        return;
                    }
                }
                else
                {
                    stuckTimer = 0f;
                }
            }
            return;
        }

        // --- 플레이어가 보일 때 ---

        // 3. 공격 사거리 체크
        Vector3 offset = target.position - owner.transform.position;
        if (offset.sqrMagnitude <= data.attackRange * data.attackRange)
        {
            owner.ChangeState(MonsterStateType.Attack);
            return;
        }

        // 4. 경로 탐색 갱신 (유저님의 데이터 변수를 활용한 최적화!)
        Vector3 moveDelta = target.position - lastTargetPos;

        if (moveDelta.sqrMagnitude > pathUpdateSqrThreshold)
        {
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
                owner.ChangeState(MonsterStateType.Search);
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    public override void Exit() { }
}