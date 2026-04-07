using UnityEngine;

public class DetectState : MonsterBaseState
{
    public DetectState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.isStopped = true; // 잠시 멈춰서 주시
        // 애니메이션: 경계하는 동작 실행
    }

    public override void FixedUpdate()
    {
        owner.scanner.Tick(); // 주변 스캔
        Transform target = owner.scanner.CurrentTarget;

        if (target != null)
        {
            // 시야에 플레이어가 있으면 경계도 상승
            owner.Alertness.Value += Time.fixedDeltaTime * 1.5f; // 상승 속도 조절

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
            owner.Alertness.Value -= Time.fixedDeltaTime * 0.5f;
            if (owner.Alertness.Value <= 0)
            {
                owner.ChangeState(MonsterStateType.Patrol);
            }
        }
    }
}