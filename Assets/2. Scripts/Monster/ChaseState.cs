using UnityEngine;

public class ChaseState : MonsterBaseState
{
    public ChaseState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.isStopped = false;
        owner.navAgent.speed = data.chaseSpeed;
    }

    public override void FixedUpdate()
    {
        owner.scanner.Tick();
        Transform target = owner.scanner.CurrentTarget;

        // 1. 타겟이 없거나, 건물 밖으로 나갔거나, 안전구역에 들어갔는지 체크
        if (target == null || !IsTargetStillValid(target.gameObject))
        {
            // 추격 중단 -> 마지막 본 위치 수색 상태로 전환
            owner.ChangeState(MonsterStateType.Search);
            return;
        }

        // 2. 타겟 추격
        owner.navAgent.SetDestination(target.position);

        // 3. 공격 사거리 체크
        float dist = Vector3.Distance(owner.transform.position, target.position);
        if (dist <= data.attackRange)
        {
            owner.ChangeState(MonsterStateType.Attack);
        }
    }

    private bool IsTargetStillValid(GameObject target)
    {
        // 팀원이 만든 플레이어 스크립트에서 Inside 여부 체크 (예시)
        // var status = target.GetComponent<PlayerStatus>();
        // if (status != null && !status.isInsideBuilding) return false;

        // 안전구역 체크
        if (owner.IsInSafeZone(target)) return false;

        return true;
    }
}