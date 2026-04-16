using UnityEngine;
using UnityEngine.AI;

public class ChaseState : MonsterBaseState
{
    private Vector3 lastTargetPos;
    private float stuckTimer;
    private float pathUpdateSqrThreshold;

    public ChaseState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();
        owner.navAgent.isStopped = false;
        owner.navAgent.speed = data.chaseSpeed;
        owner.navAgent.updateRotation = false; // 플레이어 주시를 위해 자동 회전 끔

        lastTargetPos = Vector3.positiveInfinity;
        stuckTimer = 0f;

        pathUpdateSqrThreshold = data.pathUpdateThreshold * data.pathUpdateThreshold;
    }

    protected override void OnTick()
    {
        owner.scanner.Tick();
        // 0.2초마다 문 체크 (Update에서도 하므로 보조 역할)
        if (owner.CurrentStateNet.Value == MonsterStateType.Chase)
        {
            owner.CheckAndHandleDoor();
        }
    }

    public override void Update()
    {
        base.Update();

        // 1. 문 감지
        if (owner.CurrentStateNet.Value == MonsterStateType.Chase)
        {
            if (owner.CheckAndHandleDoor()) return;
        }

        Transform target = owner.scanner.CurrentTarget;

        // 2. 타겟을 놓쳤을 때 (마지막 목격 위치로 이동)
        if (target == null || owner.IsInSafeZone(target.gameObject))
        {
            HandleTargetLost();
            return;
        }

        // 3. 타겟이 보일 때 (실시간 추격)
        stuckTimer = 0f; // 타겟이 보이면 일단 끼임 타이머 리셋
        HandleRotation(target);
        CheckAttackDistance(target);
        UpdatePath(target);
    }

    private void HandleTargetLost()
    {
        owner.navAgent.updateRotation = true; // 타겟이 없으므로 이동 방향을 바라보게 함

        // 목적지가 바뀔 때만 SetDestination 호출
        if (Vector3.Distance(owner.navAgent.destination, owner.scanner.LastSeenPosition) > 0.5f)
        {
            owner.navAgent.SetDestination(owner.scanner.LastSeenPosition);
        }

        if (!owner.navAgent.pathPending)
        {
            // [끼임 체크] 목적지가 남았는데 속도가 없다면(문에 막혔다면)
            if (owner.navAgent.velocity.sqrMagnitude < 0.2f && owner.navAgent.remainingDistance > 0.5f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 0.5f) // 0.5초만 멈춰있어도 즉시 문 체크
                {
                    if (owner.CheckAndHandleDoor()) return;

                    if (stuckTimer > 2.0f) // 2초 넘게 방법이 없으면 수색 전환
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

            // 도착 판정
            if (owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 1.0f)
            {
                owner.ChangeState(MonsterStateType.Search);
            }
        }
    }

    private void HandleRotation(Transform target)
    {
        Vector3 dir = (target.position - owner.transform.position).normalized;
        dir.y = 0;
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

    private void CheckAttackDistance(Transform target)
    {
        if (data.ceilingAttachChance > 0f) return;

        Vector3 offset = target.position - owner.transform.position;
        if (offset.sqrMagnitude <= data.attackRange * data.attackRange)
        {
            owner.ChangeState(MonsterStateType.Attack);
        }
    }

    private void UpdatePath(Transform target)
    {
        Vector3 moveDelta = target.position - lastTargetPos;
        if (moveDelta.sqrMagnitude > pathUpdateSqrThreshold)
        {
            owner.navAgent.SetDestination(target.position);
            lastTargetPos = target.position;
        }
        CheckStuck();
    }

    private void CheckStuck()
    {
        // 경로가 아예 끊겼을 경우만 체크
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
    }

    public override void Exit()
    {
        owner.navAgent.updateRotation = true;
    }
}