using UnityEngine;
using UnityEngine.AI;

public class SearchState : MonsterBaseState
{
    private float totalSearchTimer;
    private readonly float maxSearchDuration = 5f; // 총 5초간 주변 수색
    private bool isInvestigating;
    private float pauseTimer;

    public SearchState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        totalSearchTimer = 0f;
        isInvestigating = false;
        owner.navAgent.speed = data.patrolSpeed;

        // 우선 마지막으로 본 위치로 전력 질주
        owner.navAgent.SetDestination(owner.scanner.LastSeenPosition);
    }

    public override void Update()
    {
        // 수색 중 다시 발견하면 추격
        owner.scanner.Tick();
        if (owner.scanner.CurrentTarget != null)
        {
            owner.ChangeState(MonsterStateType.Chase);
            return;
        }

        totalSearchTimer += Time.deltaTime;
        if (totalSearchTimer >= maxSearchDuration)
        {
            owner.ChangeState(MonsterStateType.Patrol); // 수색 포기
            return;
        }

        // 목적지 도착 시 주변 수색 로직 시작
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance)
        {
            if (!isInvestigating)
            {
                pauseTimer += Time.deltaTime;
                // 도착 후 1.5초간 두리번거리기 (잠시 멈춤)
                if (pauseTimer > 1.5f)
                {
                    InvestigateNearby(); // 다른 곳으로 이동
                }
            }
        }
        else
        {
            // 이동 중일 때는 타이머 초기화
            pauseTimer = 0f;
            isInvestigating = false;
        }
    }

    private void InvestigateNearby()
    {
        isInvestigating = true;
        Vector3 randomDir = Random.insideUnitSphere * 6f; // 6m 반경
        randomDir += owner.transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, 6f, NavMesh.AllAreas))
        {
            owner.navAgent.SetDestination(hit.position);
        }
    }
}