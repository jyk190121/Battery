using UnityEngine;
using UnityEngine.AI;

public class ChaseState : MonsterBaseState
{
    private Vector3 lastTargetPos;
    private float stuckTimer;

    public ChaseState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();
        owner.navAgent.isStopped = false;
        owner.navAgent.speed = data.chaseSpeed;
        lastTargetPos = Vector3.zero;
        stuckTimer = 0f;
        owner.animHandler.SetSpeed(2f);
    }

    protected override void OnTick()
    {
        owner.scanner.Tick();
        Transform target = owner.scanner.CurrentTarget;

        // 1. 타겟 유효성 검사 (안전구역 등)
        if (target == null || owner.IsInSafeZone(target.gameObject))
        {
            owner.ChangeState(MonsterStateType.Search);
            return;
        }

        // 2. 문 감지 로직 (매 프레임 쏘지 않고 0.2초마다 쏴서 최적화)
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

        // 3. 공격 사거리 체크 
        Vector3 offset = target.position - owner.transform.position;
        if (offset.sqrMagnitude <= data.attackRange * data.attackRange)
        {
            owner.ChangeState(MonsterStateType.Attack);
            return;
        }

        // 4. 경로 탐색 및 예측 추격
        Vector3 movement = target.position - lastTargetPos;
        if (movement.sqrMagnitude > data.pathUpdateThreshold * data.pathUpdateThreshold)
        {
            // 플레이어 속도 유추: (움직인 거리 / 지난 시간)
            Vector3 velocity = movement / currentTickInterval;

            // 예측 지점: 현재 위치 + (속도 * 예측 가중치)
            Vector3 predictedPos = target.position + (velocity * data.predictiveChaseTime);

            owner.navAgent.SetDestination(predictedPos);
            lastTargetPos = target.position;
        }
    }

    public override void Update()
    {
        base.Update(); // OnTick 실행
        stuckTimer += Time.deltaTime;

        // 길 찾기 실패/끼임 체크
        if (!owner.navAgent.pathPending &&
            (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial || owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid))
        {
            if (stuckTimer > 1.5f) owner.ChangeState(MonsterStateType.Search);
        }
        else { stuckTimer = 0f; }
    }

    public override void Exit() { }

    //public override void FixedUpdate()
    //{
    //    stuckTimer += Time.fixedDeltaTime;
    //    aiTickTimer += Time.fixedDeltaTime;

    //    if (aiTickTimer >= data.aiTickInterval)
    //    {
    //        aiTickTimer = 0f;
    //        ThinkAndAct();
    //    }
    //    //// 2. 문 감지 로직 
    //    //RaycastHit hit;

    //    //if (Physics.Raycast(owner.transform.position + Vector3.up, owner.transform.forward, out hit, 2.5f))
    //    //{
    //    //    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Door"))
    //    //    {
    //    //        DoorController door = hit.collider.GetComponent<DoorController>();

    //    //        if (door != null && !door.isOpen)
    //    //        {
    //    //            owner.TargetDoor = door;
    //    //            owner.ChangeState(MonsterStateType.InteractDoor);
    //    //            return;
    //    //        }
    //    //    }
    //    //}

    //    //// 3. 경로 탐색 최적화
    //    //if (Vector3.Distance(target.position, lastTargetPos) > pathUpdateThreshold)
    //    //{
    //    //    owner.navAgent.SetDestination(target.position);
    //    //    lastTargetPos = target.position;
    //    //}

    //    //// 4. 도달 불가 예외 처리
    //    //if (!owner.navAgent.pathPending)
    //    //{
    //    //    if (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial ||
    //    //        owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
    //    //    {
    //    //        stuckTimer += Time.fixedDeltaTime;
    //    //        if (stuckTimer > 1.5f)
    //    //        {
    //    //            Debug.Log("플레이어 도달 불가. 수색 상태로 전환.");
    //    //            owner.ChangeState(MonsterStateType.Search);
    //    //            return;
    //    //        }
    //    //    }
    //    //    else
    //    //    {
    //    //        stuckTimer = 0f;
    //    //    }
    //    //}

    //    //// 5. 공격 사거리 체크
    //    //float dist = Vector3.Distance(owner.transform.position, target.position);
    //    //if (dist <= data.attackRange)
    //    //{
    //    //    owner.ChangeState(MonsterStateType.Attack);
    //    //}
    //}



    ////private void ThinkAndAct()
    ////{
    ////    owner.scanner.Tick();
    ////    Transform target = owner.scanner.CurrentTarget;

    ////    // [유효성 체크] 플레이어가 없거나 안전구역이면 수색 전환
    ////    if (target == null || !IsTargetStillValid(target.gameObject))
    ////    {
    ////        owner.ChangeState(MonsterStateType.Search);
    ////        return;
    ////    }

    ////    // [문 감지 로직] 정면에 문이 있는지 레이캐스트
    ////    RaycastHit hit;
    ////    if (Physics.Raycast(owner.transform.position + Vector3.up, owner.transform.forward, out hit, 2.5f))
    ////    {
    ////        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Door"))
    ////        {
    ////            DoorController door = hit.collider.GetComponent<DoorController>();
    ////            if (door != null && !door.isOpen)
    ////            {
    ////                owner.TargetDoor = door;
    ////                owner.ChangeState(MonsterStateType.InteractDoor);
    ////                return;
    ////            }
    ////        }
    ////    }

    //    // [사거리 체크] 거리 연산 최적화
    //    Vector3 offsetToTarget = target.position - owner.transform.position;
    //    float sqrDist = offsetToTarget.sqrMagnitude;
    //    if (sqrDist <= data.attackRange * data.attackRange)
    //    {
    //        owner.ChangeState(MonsterStateType.Attack);
    //        return;
    //    }

    //    // [예측 추격 & 경로 최적화] 타겟이 움직인 방향을 계산해서 앞질러감
    //    Vector3 offsetToLastPos = target.position - lastTargetPos;
    //    if (offsetToLastPos.sqrMagnitude > data.pathUpdateThreshold * data.pathUpdateThreshold)
    //    {
    //        // 타겟 이동 속도 계산
    //        Vector3 targetMoveDir = (target.position - lastTargetPos) / data.aiTickInterval;

    //        // 예측 지점 설정
    //        Vector3 predictedPos = target.position + (targetMoveDir * data.predictiveChaseTime);

    //        owner.navAgent.SetDestination(predictedPos);
    //        lastTargetPos = target.position;
    //    }

    //    // [끼임 방지] 경로가 끊겼을 경우 처리
    //    if (!owner.navAgent.pathPending)
    //    {
    //        if (owner.navAgent.pathStatus == NavMeshPathStatus.PathPartial ||
    //            owner.navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
    //        {
    //            if (stuckTimer > 1.5f)
    //            {
    //                owner.ChangeState(MonsterStateType.Search);
    //            }
    //        }
    //        else
    //        {
    //            stuckTimer = 0f;
    //        }
    //    }
    //}

    private bool IsTargetStillValid(GameObject target)
    {
        if (owner.IsInSafeZone(target)) return false;
        return true;
    }
}