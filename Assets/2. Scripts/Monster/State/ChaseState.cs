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
        owner.animHandler.SetSpeed(data.chaseSpeed);
    }

    public override void FixedUpdate()
    {
        owner.scanner.Tick();
        Transform target = owner.scanner.CurrentTarget;

        // 1. 타겟 유효성 검사
        if (target == null || !IsTargetStillValid(target.gameObject))
        {
            owner.ChangeState(MonsterStateType.Search);
            return;
        }

   
        // 2. 문 감지 로직 
        RaycastHit hit;

        if (Physics.Raycast(owner.transform.position + Vector3.up, owner.transform.forward, out hit, 2.5f))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Door"))
            {
                DoorController door = hit.collider.GetComponent<DoorController>();

                if (door != null && !door.isOpen)
                {
                    owner.TargetDoor = door;
                    owner.ChangeState(MonsterStateType.InteractDoor);
                    return;
                }
            }
        }

        // 3. 경로 탐색 최적화
        if (Vector3.Distance(target.position, lastTargetPos) > pathUpdateThreshold)
        {
            owner.navAgent.SetDestination(target.position);
            lastTargetPos = target.position;
        }

        // 4. 도달 불가 예외 처리
        if (!owner.navAgent.pathPending)
        {
            if (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial ||
                owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 1.5f)
                {
                    Debug.Log("플레이어 도달 불가. 수색 상태로 전환.");
                    owner.ChangeState(MonsterStateType.Search);
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        // 5. 공격 사거리 체크
        float dist = Vector3.Distance(owner.transform.position, target.position);
        if (dist <= data.attackRange)
        {
            owner.ChangeState(MonsterStateType.Attack);
        }
    }

    private bool IsTargetStillValid(GameObject target)
    {
        if (owner.IsInSafeZone(target)) return false;
        return true;
    }
}