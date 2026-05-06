using UnityEngine;

public abstract class MonsterBaseState : IState
{
    protected MonsterController owner;
    protected MonsterData data;

    private float tickTimer;
    // 상태마다 주기를 다르게 설정할 수 있도록 변수화
    protected float currentTickInterval;    // AI 사고 주기 (최적화)

    public MonsterBaseState(MonsterController owner)
    {
        this.owner = owner;
        this.data = owner.monsterData;
        this.currentTickInterval = data.aiTickInterval;
    }

    public virtual void Enter()
    {
        // 여러 마리의 몬스터가 동일한 프레임에 동시에 연산하는 현상(CPU 스파이크)을 방지
        tickTimer = Random.Range(0f, currentTickInterval);
    }

    public virtual void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer >= currentTickInterval)
        {
            tickTimer = 0f;
            OnTick(); // 0.2초마다 실행될 함수
        }
    }

    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    // 자식 스크립트들이 "무거운 연산"을 작성할 곳
    protected virtual void OnTick() { }

    // =========================================================
    // [공통 헬퍼 함수] 하이브리드 지형 인지 회전 시스템
    // =========================================================
    /// <summary>
    /// 벽에 가려졌을 때는 코너(SteeringTarget)를, 시야가 확보되면 타겟을 직접 바라보며 회전합니다.
    /// ChaseState뿐만 아니라 Investigate, Patrol 등 모든 상태에서 이 함수를 호출할 수 있습니다.
    /// </summary>
    protected void HandleHybridRotation(Transform target)
    {
        // 타겟이 없거나 길을 아직 찾지 못했다면 회전 보류
        if (target == null || !owner.navAgent.hasPath) return;

        Vector3 lookPosition;

        // [0-Cost 시야 판별 로직]
        // 비싼 Raycast를 쏘지 않고, NavAgent가 가리키는 다음 꺾이는 코너(steeringTarget)와
        // 타겟의 실제 위치(target.position)의 거리를 비교합니다.
        // 둘이 거의 일치한다면(1.5f 이내), 중간에 꺾이는 벽이 없다는 뜻(시야 확보)입니다.
        float distToSteeringTarget = Vector3.Distance(owner.navAgent.steeringTarget, target.position);
        bool hasLineOfSight = distToSteeringTarget < 1.5f;

        if (hasLineOfSight)
        {
            // 시야 확보 (벽 없음) -> 타겟을 직접 노려봄
            lookPosition = target.position;
        }
        else
        {
            // 벽에 가려짐 -> 벽에 머리를 비비지 않도록 에이전트의 다음 이동 방향(코너)을 바라봄
            lookPosition = owner.navAgent.steeringTarget;
        }

        // Y축(상하) 회전을 강제 고정하여 몬스터가 바닥이나 하늘을 보며 기울어지는 현상 차단
        Vector3 dir = (lookPosition - owner.transform.position).normalized;
        dir.y = 0;

        if (dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            // 부드럽게 몬스터의 무게감을 살려 회전 (수치는 필요에 따라 data에서 끌어와도 좋습니다)
            owner.transform.rotation = Quaternion.RotateTowards(
                owner.transform.rotation,
                targetRotation,
                Time.deltaTime * 500f
            );
        }
    }
}