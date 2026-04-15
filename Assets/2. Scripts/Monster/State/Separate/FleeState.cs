using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class FleeState : MonsterBaseState
{
    private float fleeTimer;
    private List<Transform> farWaypoints = new List<Transform>(32);

    public FleeState(MonsterController owner) : base(owner)
    {
        this.currentTickInterval = 0.5f;
    }

    public override void Enter()
    {
        base.Enter();
        fleeTimer = 0f;

        if (NavMesh.SamplePosition(owner.transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            owner.transform.position = hit.position;
        }

        owner.navAgent.enabled = true;
        owner.navAgent.isStopped = false;

        // 2. 도망갈 때는 추격 속도보다 더 빠르게(1.5배) 세팅합니다.
        owner.navAgent.speed = data.chaseSpeed * 1.5f;

        Debug.Log("<color=blue>[Snare Flea]</color> 으악! 맞았다! 전속력으로 도망갑니다!");

        // 3. 가장 안전한(먼) 목적지를 찾아 뛰기 시작합니다.
        SetFleeDestination();
    }

    protected override void OnTick()
    {
        if (owner.CheckAndHandleDoor()) return;
    }

    public override void Update()
    {
        base.Update();

        fleeTimer += Time.deltaTime;

        // 설정된 도망 시간(fleeDuration)이 끝나면 다시 순찰 모드로 돌아갑니다.
        if (fleeTimer >= data.fleeDuration)
        {
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 목적지에 거의 다 도착했는데 아직 도망 시간이 남았다면?
        // 다른 먼 곳을 다시 찾아서 계속 도망갑니다.
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 1.0f)
        {
            SetFleeDestination();
        }
    }

    /// <summary>
    /// 현재 위치에서 일정 거리 이상 떨어진 곳 중 무작위로 하나를 선택하여 예측 불가능하게 도망칩니다.
    /// </summary>
    private void SetFleeDestination()
    {
        if (owner.waypointManager == null || owner.waypointManager.waypoints.Count == 0) return;

        farWaypoints.Clear();

        Transform bestFallbackPoint = null;
        float maxDistSqr = -1f;

        Vector3 currentPos = owner.transform.position;
        float safeDistance = 15f;
        float safeDistanceSqr = safeDistance * safeDistance;
        var waypoints = owner.waypointManager.waypoints;
        int count = waypoints.Count;

        for (int i = 0; i < count; i++)
        {
            Transform wp = waypoints[i];
            float distSqr = (wp.position - currentPos).sqrMagnitude;

            if (distSqr >= safeDistanceSqr)
            {
                farWaypoints.Add(wp);
            }

            if (distSqr > maxDistSqr)
            {
                maxDistSqr = distSqr;
                bestFallbackPoint = wp;
            }
        }

        Transform targetPoint = farWaypoints.Count > 0
            ? farWaypoints[Random.Range(0, farWaypoints.Count)]
            : bestFallbackPoint;

        // 3. 네비게이션 에이전트에게 최종 목적지 하달
        if (targetPoint != null)
        {
            owner.navAgent.SetDestination(targetPoint.position);

            float actualDist = Mathf.Sqrt((targetPoint.position - currentPos).sqrMagnitude);
            Debug.Log($"<color=blue>[Snare Flea]</color> 예측 불허 {targetPoint.name} 지점을 향해 도주 중 (거리: {actualDist:F1}m)");
        }
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
}