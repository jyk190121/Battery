using UnityEngine;

public class PatrolState : MonsterBaseState
{
    private WaypointManager waypointManager;
    private bool isWaiting;
    private float waitTimer;
    private float currentWaitDuration;

    public PatrolState(MonsterController owner) : base(owner)
    {
        // 하이어라키에서 WaypointManager를 찾아옵니다. (매니저가 하나만 있다고 가정)
        waypointManager = Object.FindAnyObjectByType<WaypointManager>();
    }

    public override void Enter()
    {
        owner.navAgent.speed = data.patrolSpeed;
        owner.navAgent.isStopped = false;
        isWaiting = false;
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
        Transform nextPoint = waypointManager?.GetRandomWaypoint();
        if (nextPoint != null)
        {
            owner.navAgent.SetDestination(nextPoint.position);
            owner.navAgent.isStopped = false;
            // 이동 애니메이션 재생 (예: IsWalking = true)
        }
    }

    private void CheckArrival()
    {
        // 목적지에 거의 도착했는지 확인
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

        owner.navAgent.isStopped = true;
        // 이동 애니메이션 중단, 대기/두리번 애니메이션 재생
        // owner.animHandler.SetMoving(false);
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