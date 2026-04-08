using UnityEngine;

public class PatrolState : MonsterBaseState
{
    private bool isWaiting;
    private float waitTimer;
    private float currentWaitDuration;

    // 끼임 방지용 변수
    private float stuckTimer;
    private readonly float maxMoveTime = 10f;   // 10초 이상 같은 목적지로 이동하면 끼인 것으로 간주    

    public PatrolState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.speed = data.patrolSpeed;
        owner.navAgent.isStopped = false;
        isWaiting = false;
        stuckTimer = 0f; // 이동 시작 시 타이머 초기화

        MoveToNextPoint();
    }

    public override void Update()
    {
        // 1. 감지 체크 (항상 우선)
        owner.scanner.Tick();
        if (owner.scanner.CurrentTarget != null)
        {
            owner.ChangeState(MonsterStateType.Detect);
            return;
        }

        // 2. 대기 중인지 이동 중인지 판단
        if (isWaiting)
        {
            HandleWaiting();
        }
        else
        {
            CheckArrival();
        }
    }

    private void MoveToNextPoint()
    {
        Transform nextPoint = owner.waypointManager?.GetRandomWaypoint();
        owner.animHandler.SetSpeed(1f);

        if (nextPoint != null)
        {
            Debug.Log($"[{nextPoint.name}] 지점으로 이동 시작");
            owner.navAgent.SetDestination(nextPoint.position);
            owner.navAgent.isStopped = false;

            stuckTimer = 0f;    // 새 목적지로 갈 때 끼임 타이머 초기화
            // 이동 애니메이션 재생 (예: IsWalking = true)
        }
    }

    private void CheckArrival()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= maxMoveTime)
        {
            Debug.LogWarning("몬스터가 지형에 끼었거나 목적지 도달에 실패했습니다. 새로운 경로를 탐색합니다.");
            MoveToNextPoint(); // 끼임 판정 시 즉시 다른 목적지로 강제 이동
            return;
        }

        // 목적지에 도착했는지 확인 (remainingDistance가 멈춤 거리보다 작아질 때)
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance)
        {
            StartWaiting();
        }
    }

    private void StartWaiting()
    {
        isWaiting = true;
        waitTimer = 0f;
        currentWaitDuration = Random.Range(data.minWaitTime, data.maxWaitTime);
        owner.animHandler.SetSpeed(0f);

        owner.navAgent.isStopped = true;
    }

    private void HandleWaiting()
    {
        waitTimer += Time.deltaTime;

        // 대기 시간이 끝나면 다음 지점으로 이동
        if (waitTimer >= currentWaitDuration)
        {
            isWaiting = false;
            MoveToNextPoint();
        }
    }
}