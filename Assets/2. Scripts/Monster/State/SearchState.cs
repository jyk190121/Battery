using UnityEngine;
using UnityEngine.AI;

public class SearchState : MonsterBaseState
{
    private float totalSearchTimer;             // 전체 수색 시간 제한
    private bool isInvestigating;               // 특정 지점을 조사 중인지 여부
    private float pauseTimer;                   // 도착 후 두리번거리는 시간
    private int searchAttemptCount;             // 수색 시도 횟수

    public SearchState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        totalSearchTimer = 0f;
        isInvestigating = false;
        pauseTimer = 0f;
        searchAttemptCount = 0;

        owner.animHandler.SetSearching(false);
        owner.animHandler.SetSpeed(1f);

        owner.navAgent.speed = data.patrolSpeed;
        owner.navAgent.isStopped = false; 


        if (owner.scanner.LastSeenPosition == Vector3.zero)
        {
            owner.ChangeState(MonsterStateType.Patrol); 
            return;
        }

        owner.navAgent.SetDestination(owner.scanner.LastSeenPosition);
        Debug.Log($"[수색 시작] 마지막으로 목격된 위치({owner.scanner.LastSeenPosition})로 이동.");
    }

    public override void Update()
    {
        // 수색 중 다시 발견하면 추격
        owner.scanner.Tick();
        if (owner.scanner.CurrentTarget != null)
        {
            Debug.Log("[수색 성공] 플레이어를 다시 발견. 추격을 재개.");
            owner.ChangeState(MonsterStateType.Chase);
            return;
        }

        totalSearchTimer += Time.deltaTime;

        if (totalSearchTimer >= data.maxSearchDuration)
        {
            Debug.Log("[수색 포기] 아무것도 찾지 못했습니다. 순찰로 돌아갑니다.");
            owner.ChangeState(MonsterStateType.Patrol); // 수색 포기
            return;
        }

        if (owner.CheckAndHandleDoor()) return;

        CheckSearchArrival();
    }

    private void CheckSearchArrival()
    {
        // 목적지에 도달했는지 확인
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance)
        {
            if (!isInvestigating)
            {
                pauseTimer += Time.deltaTime;
                owner.animHandler.SetSpeed(0f); // 조사할 때 잠시 멈춤
                owner.animHandler.SetSearching(true);

                // 도착 후 설정된 시간(예: 1.5초) 동안 두리번거리기
                if (pauseTimer > data.searchPauseDuration)
                {
                    InvestigateNearby(); // 다음 주변 지점으로 이동
                }
            }
        }
        else
        {
            // 이동 중일 때는 애니메이션 속도를 걷기로 설정
            owner.animHandler.SetSpeed(1f);
            owner.animHandler.SetSearching(false);
            pauseTimer = 0f;
            isInvestigating = false;
        }
    }

    private void InvestigateNearby()
    {
        isInvestigating = true;
        searchAttemptCount++;

        // 탐색 범위: 횟수가 늘어날수록 더 넓은 범위를 찾아봅니다 (5m -> 7m -> 9m...)
        float searchRadius = 5f + (searchAttemptCount * 2f);

        Vector3 randomDir = Random.insideUnitSphere * searchRadius;
        randomDir += owner.scanner.LastSeenPosition;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, searchRadius, NavMesh.AllAreas))
        {
            Debug.Log($"[수색 진행] {searchAttemptCount}번째 탐색 지점으로 이동 중");
            owner.navAgent.SetDestination(hit.position);
            owner.navAgent.isStopped = false;
        }
    }

    public override void Exit()
    {
        owner.animHandler.SetSearching(false); // 두리번 애니메이션 종료
    }
}