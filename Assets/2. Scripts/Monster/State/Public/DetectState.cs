using UnityEngine;

public class DetectState : MonsterBaseState
{
    public DetectState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.isStopped = true; // 잠시 멈춰서 주시
        // 애니메이션: 경계하는 동작 실행

        // NavMeshAgent의 자동 회전을 꺼서 수동 회전(Slerp)과 충돌하지 않게 함
        owner.navAgent.updateRotation = false;
    }

    protected override void OnTick()
    {
        owner.scanner.Tick();

        // 플레이어를 놓쳤는지 확인하는 무거운 로직은 여기서 처리
        if (owner.scanner.CurrentTarget == null)
        {
            owner.Alertness.Value -= 0.1f; // 예시 수치
            if (owner.Alertness.Value <= 0) owner.ChangeState(MonsterStateType.Patrol);
        }
    }

    public override void FixedUpdate()
    {
        if (!owner.IsServer) return;

        Transform target = owner.scanner.CurrentTarget;

        if (target != null)
        {
            // 시야에 플레이어가 있으면 경계도 상승
            owner.ServerAlertness += Time.fixedDeltaTime * data.alertnessIncreaseRate;

            // 타겟 방향으로 천천히 회전
            Vector3 dir = (target.position - owner.transform.position).normalized;
            owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation,
                Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)), Time.fixedDeltaTime * 5f);

            if (owner.Alertness.Value >= 1.0f)
            {
                owner.ChangeState(MonsterStateType.Chase);
            }
        }
        else
        {
            // 플레이어를 놓치면 경계도 감소
            owner.ServerAlertness -= Time.fixedDeltaTime * data.alertnessDecreaseRate;
            if (owner.Alertness.Value <= 0)
            {
                owner.ChangeState(MonsterStateType.Patrol);
            }
        }
    }

    public override void Exit()
    {
        // 상태를 나갈 때 다시 자동 회전을 켜줌
        owner.navAgent.updateRotation = true;
    }
}