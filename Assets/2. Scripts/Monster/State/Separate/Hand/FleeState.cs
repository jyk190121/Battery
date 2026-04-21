using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 몬스터가 플레이어에게 타격을 입고 패닉에 빠져 무작위 안전지대로 도망치는 상태입니다. (예: 올무벼룩)
/// </summary>
public class FleeState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private float _fleeTimer;
    private List<Transform> _farWaypoints = new List<Transform>(32);


    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public FleeState(MonsterController owner) : base(owner)
    {
        this.currentTickInterval = 0.5f;
    }

    public override void Enter()
    {
        base.Enter();
        _fleeTimer = 0f;

        // 1. 천장에서 떨어졌거나 물리적으로 튕겨 나갔을 경우를 대비해, 
        // 현재 위치에서 가장 가까운 NavMesh(바닥) 위로 위치를 보정합니다.
        if (NavMesh.SamplePosition(owner.transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            owner.transform.position = hit.position;
        }

        // 2. 네비게이션 에이전트 가동
        owner.navAgent.enabled = true;
        owner.navAgent.isStopped = false;

        // 3. 도망갈 때는 일반 추격 속도보다 훨씬 빠르게(1.5배) 세팅
        owner.navAgent.speed = data.chaseSpeed * 1.5f;

        Debug.Log("<color=blue>[Snare Flea]</color> 으악! 맞았다! 전속력으로 도망갑니다!");

        // 4. 가장 안전한(먼) 목적지 후보군을 찾아 뛰기 시작합니다.
        SetFleeDestination();
    }

    public override void Exit()
    {
        base.Exit();

        // 도망 상태가 끝나면 다시 정상적인 순찰 속도로 되돌려 놓습니다.
        if (owner.navAgent != null && owner.navAgent.isOnNavMesh)
        {
            owner.navAgent.speed = data.patrolSpeed;
        }

        Debug.Log("<color=yellow>[Snare Flea]</color> 안정이 되었습니다. 다시 사냥을 준비합니다.");
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 도망 지속 시간을 체크하고, 목적지에 도착하면 다른 곳으로 다시 도망갑니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        _fleeTimer += Time.deltaTime;

        // 1. 설정된 도망 시간(fleeDuration)이 끝나면 진정하고 다시 순찰 모드로 돌아갑니다.
        if (_fleeTimer >= data.fleeDuration)
        {
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 2. 목적지에 거의 다 도착했는데 아직 도망 시간이 남았다면?
        // 멈추지 않고 다른 먼 곳을 다시 찾아서 계속 도망갑니다.
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 1.0f)
        {
            SetFleeDestination();
        }
    }

    /// <summary>
    /// 0.5초마다 실행: 도망치는 경로상에 닫힌 문이 있는지 검사합니다.
    /// </summary>
    protected override void OnTick()
    {
        // 도망치다가 문에 막히면 문을 엽니다.
        if (owner.CheckAndHandleDoor()) return;
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 현재 위치에서 일정 거리 이상 떨어진 곳 중 무작위로 하나를 선택하여 예측 불가능하게 도망칩니다.
    /// </summary>
    private void SetFleeDestination()
    {
        if (owner.waypointManager == null || owner.waypointManager.waypoints.Count == 0) return;

        _farWaypoints.Clear();

        Transform bestFallbackPoint = null;
        float maxDistSqr = -1f;

        Vector3 currentPos = owner.transform.position;
        float safeDistance = 15f;
        float safeDistanceSqr = safeDistance * safeDistance; // 연산 최적화를 위한 제곱값 사용

        var waypoints = owner.waypointManager.waypoints;
        int count = waypoints.Count;

        // 1. 모든 웨이포인트를 순회하며 안전거리(15m) 이상 떨어진 곳들을 수집
        for (int i = 0; i < count; i++)
        {
            Transform wp = waypoints[i];
            float distSqr = (wp.position - currentPos).sqrMagnitude;

            // 15m 이상 떨어진 곳은 '도망 후보지'에 모두 등록
            if (distSqr >= safeDistanceSqr)
            {
                _farWaypoints.Add(wp);
            }

            // 만약 15m 이상 떨어진 곳이 단 하나도 없을 경우를 대비해, 무조건 가장 먼 곳을 기록해 둠
            if (distSqr > maxDistSqr)
            {
                maxDistSqr = distSqr;
                bestFallbackPoint = wp;
            }
        }

        // 2. 후보지가 있다면 그중에서 랜덤으로 하나 선택, 없다면 아까 기록해둔 가장 먼 곳을 선택
        Transform targetPoint = _farWaypoints.Count > 0
            ? _farWaypoints[Random.Range(0, _farWaypoints.Count)]
            : bestFallbackPoint;

        // 3. 네비게이션 에이전트에게 최종 목적지 하달
        if (targetPoint != null)
        {
            owner.navAgent.SetDestination(targetPoint.position);

            // 로그 출력을 위해 제곱근(Sqrt)으로 실제 거리를 다시 계산하여 보여줌
            float actualDist = Mathf.Sqrt((targetPoint.position - currentPos).sqrMagnitude);
            Debug.Log($"<color=blue>[Snare Flea]</color> 예측 불허 {targetPoint.name} 지점을 향해 도주 중 (거리: {actualDist:F1}m)");
        }
    }
}