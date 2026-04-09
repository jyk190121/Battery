using UnityEngine;

public class AttackState : MonsterBaseState
{
    private float attackTimer;

    public AttackState(MonsterController owner) : base(owner) 
    {
        // 공격은 찰나의 순간이 중요하므로 0.05초 주기로 생각하게 함
        this.currentTickInterval = data.fastTickInterval;
    }

    public override void Enter()
    {
        base.Enter();
        owner.navAgent.isStopped = true;    // 공격 시 멈춤
        //owner.navAgent.ResetPath();
        //owner.navAgent.velocity = Vector3.zero;
        //owner.animHandler.SetSpeed(0f);

        attackTimer = data.attackCooldown;  // 즉시 공격 가능하게 설정하거나 대기
    }

    protected override void OnTick()
    {
        // 공격 중에도 타겟이 사거리를 벗어났는지 아주 빠르게(0.05초마다) 체크
        Transform target = owner.scanner.CurrentTarget;
        if (target == null)
        {
            owner.ChangeState(MonsterStateType.Chase);
            return;
        }

        // 공격 애니메이션이 끝난 후 사거리 체크
        if (attackTimer >= data.attackAnimDuration)
        {
            float sqrDist = (target.position - owner.transform.position).sqrMagnitude;
            float threshold = data.attackRange + 0.5f;
            if (sqrDist > threshold * threshold)
            {
                owner.ChangeState(MonsterStateType.Chase);
            }
        }
    }

    public override void Update()
    {
        base.Update();
        attackTimer += Time.deltaTime;

        // 유도 회전
        if (attackTimer < data.attackAnimDuration)
        {
            Transform target = owner.scanner.CurrentTarget;
            if (target != null)
            {
                Vector3 dir = (target.position - owner.transform.position).normalized;
                Quaternion targetRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
                owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, targetRot, Time.deltaTime * 8f);
            }
        }

        // 쿨타임이 차면 공격 '애니메이션'만 실행. 데미지는 주지 않음.
        if (attackTimer >= data.attackCooldown)
        {
            owner.animHandler.PlayAttack();
            attackTimer = 0f; // 쿨타임 초기화
        }
    }

    // MonsterController에서 애니메이션 이벤트 발생 시 호출하는 함수
    public void ApplyDamageToTarget()
    {
        Transform target = owner.scanner.CurrentTarget;
        if (target != null)
        {
            float hitThreshold = data.attackRange + 1.0f;
            if ((target.position - owner.transform.position).sqrMagnitude <= hitThreshold * hitThreshold)
            {
                Debug.Log($"[데미지 발생] {data.attackDamage} 피해");
            }
        }
    }
}