using UnityEngine;
using UnityEngine.AI;

public class SearchState : MonsterBaseState
{
    private float totalSearchTimer;
    private bool isInvestigating;
    private float pauseTimer;
    private int searchAttemptCount;                 // 몇 군데나 찾아봤는지 기록

    public SearchState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        totalSearchTimer = 0f;
        isInvestigating = false;
        pauseTimer = 0f;
        searchAttemptCount = 0;

        owner.animHandler.SetSpeed(0f); // 멈춤
        owner.animHandler.SetSearching(true); // 두리번 애니메이션 시작

        owner.navAgent.speed = data.patrolSpeed;

        // 우선 마지막으로 본 위치로 이동
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

        // 목적지 도착 시 주변 수색 로직 시작
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance)
        {
            if (!isInvestigating)
            {
                pauseTimer += Time.deltaTime;
                // 도착 후 1.5초간 두리번거리기 (잠시 멈춤)
                if (pauseTimer > data.searchPauseDuration)
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

    public override void Exit()
    {
        owner.animHandler.SetSearching(false); // 두리번 애니메이션 종료
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
        }
    }
}