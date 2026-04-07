using UnityEngine;
using UnityEngine.AI;

public class ChaseState : MonsterBaseState
{
    private Vector3 lastTargetPos;
    private readonly float pathUpdateThreshold = 1.0f; // 타겟이 1m 이상 이동해야 경로 재계산
    private float stuckTimer;

    public ChaseState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.isStopped = false;
        owner.navAgent.speed = data.chaseSpeed;
        lastTargetPos = Vector3.zero;
        stuckTimer = 0f;
    }

    public override void FixedUpdate()
    {
        owner.scanner.Tick();
        Transform target = owner.scanner.CurrentTarget;

        // 타겟이 없거나, 건물 밖으로 나갔거나, 안전구역에 들어갔는지 체크
        if (target == null || !IsTargetStillValid(target.gameObject))
        {
            // 추격 중단 -> 마지막 본 위치 수색 상태로 전환
            owner.ChangeState(MonsterStateType.Search);
            return;
        }

        // 경로 탐색 최적화: 타겟이 일정 거리 이상 움직였을 때만 NavMesh 재계산
        if (Vector3.Distance(target.position, lastTargetPos) > pathUpdateThreshold)
        {
            owner.navAgent.SetDestination(target.position);
            lastTargetPos = target.position;
        }

        // 도달 불가 예외 처리
        if (!owner.navAgent.pathPending)
        {
            // PathPartial(도중에 끊김) 또는 PathInvalid(아예 갈 수 없음) 판정 시
            if (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial ||
                owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 1.5f) // 1.5초간 길이 없으면 포기
                {
                    Debug.Log("플레이어 도달 불가 (박스 위 등). 수색 상태로 전환.");
                    owner.ChangeState(MonsterStateType.Search);
                    return;
                }
            }
            else
            {
                stuckTimer = 0f; // 길을 다시 찾으면 타이머 초기화
            }
        }

        // 공격 사거리 체크
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