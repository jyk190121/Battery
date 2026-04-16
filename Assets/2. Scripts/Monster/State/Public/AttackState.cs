using UnityEngine;

public class AttackState : MonsterBaseState
{
    private float attackTimer;
    private bool isAnimationPlaying;

    private bool isExiting;
    public AttackState(MonsterController owner) : base(owner) 
    {
        // 공격은 찰나의 순간이 중요하므로 0.05초 주기로 생각하게 함
        this.currentTickInterval = data.fastTickInterval;
    }

    public override void Enter()
    {
        base.Enter();
        owner.navAgent.isStopped = true;    // 공격 시 멈춤
        owner.navAgent.ResetPath();
        owner.navAgent.velocity = Vector3.zero;

        attackTimer = data.attackCooldown;  // 즉시 공격 가능하게 설정하거나 대기
        isAnimationPlaying = false;
        isExiting = false;
    }

    protected override void OnTick()
    {
        if (isExiting || owner.CurrentStateNet.Value != MonsterStateType.Attack) return;

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

            // 플레이어가 도망가서 사거리 밖이라면?
            if (sqrDist > threshold * threshold)
            {
                ExitToChase();
                return;
            }
            else
            {
                // [버그 수정 3] 여전히 사거리 안에 있을 때만 다음 공격을 위해 락(Lock)을 풀어줍니다.
                // (도망갔는데 풀어버리면 Update에서 공격을 또 실행해버리는 버그 방지)
                isAnimationPlaying = false;
            }
        }
    }

    public override void Update()
    {
        base.Update();
        attackTimer += Time.deltaTime;

        Transform target = owner.scanner.CurrentTarget;

        // 유도 회전
        if (attackTimer < data.attackAnimDuration)
        {
            if (target != null)
            {
                LookAtTarget();
            }
        }

        // 쿨타임이 차면 공격 '애니메이션'만 실행. 데미지는 주지 않음.
        if (!isAnimationPlaying && attackTimer >= data.attackCooldown)
        {
            ExecuteAttack();
        }
    }

    private void LookAtTarget()
    {
        Transform target = owner.scanner.CurrentTarget;
        if (target == null) return;

        Vector3 dir = (target.position - owner.transform.position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
        owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, targetRot, Time.deltaTime * 8f);
    }

    private void ExecuteAttack()
    {
        isAnimationPlaying = true;
        attackTimer = 0f; // 여기서 리셋해도 이제 OnTick의 조건이 다시 돌아가므로 안전함
        owner.animHandler.PlayAttack();
    }

    // MonsterController에서 애니메이션 이벤트 발생 시 호출하는 함수
    public void ApplyDamageToTarget()
    {
        Transform target = owner.scanner.CurrentTarget;
        if (target == null) return;

        if (target != null)
        {

            float hitThreshold = data.attackRange + 1.0f;
            if ((target.position - owner.transform.position).sqrMagnitude <= hitThreshold * hitThreshold)
            {
                if (target.TryGetComponent<PlayerController>(out var controller))
                {
                    controller.TakeDamageServerRpc(data.attackDamage);
                    Debug.Log($"[데미지 발생] {data.attackDamage} 피해 전달됨");
                }
            }
        }
    }

    /// <summary>
    /// 안전하게 추격 상태로 넘어가는 헬퍼 함수
    /// </summary>
    private void ExitToChase()
    {
        isExiting = true;
        owner.ChangeState(MonsterStateType.Chase);
    }
}