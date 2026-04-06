using UnityEngine;
using UnityEngine.AI;

public class PatrolState : MonsterBaseState
{
    private Vector3 targetDestination;

    public PatrolState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.speed = data.patrolSpeed;
        owner.navAgent.isStopped = false;
        MoveToRandomWaypoint();
    }

    public override void Update()
    {
        // 1. 플레이어 감지 체크 (서버에서만 수행)
        owner.scanner.Tick();
        if (owner.scanner.CurrentTarget != null)
        {
            owner.ChangeState(MonsterStateType.Detect);
            return;
        }

        // 2. 목적지에 도착하면 새로운 목적지 설정
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance < 0.5f)
        {
            MoveToRandomWaypoint();
        }
    }

    private void MoveToRandomWaypoint()
    {
        // 환경 분석기(Scanner)나 별도의 WaypointManager에서 지점을 가져와야 합니다.
        // 여기서는 임시로 현재 위치 주변의 랜덤한 위치를 잡습니다.
        Vector3 randomDirection = Random.insideUnitSphere * 20f;
        randomDirection += owner.transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 20f, NavMesh.AllAreas))
        {
            targetDestination = hit.position;
            owner.navAgent.SetDestination(targetDestination);
        }
    }
}