using UnityEngine;

public class AttackState : MonsterBaseState
{
    private float attackTimer;

    public AttackState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.isStopped = true;    // 공격 시 멈춤
        attackTimer = data.attackCooldown;  // 즉시 공격 가능하게 설정하거나 대기

        Transform target = owner.scanner.CurrentTarget; // 타겟을 바라보게 회전
        if (target != null)
        {
            Vector3 dir = (target.position - owner.transform.position).normalized;
            owner.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
        }

    }

    public override void Update()
    {
        attackTimer += Time.deltaTime;

        Transform target = owner.scanner.CurrentTarget;

        // 타겟이 없거나 사거리 밖이면 다시 추격
        if (target == null || Vector3.Distance(owner.transform.position, target.position) > data.attackRange + 0.5f)
        {
            owner.ChangeState(MonsterStateType.Chase);
            return;
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
            // 한 번 더 사거리 체크 (애니메이션 재생 도중 도망갔을 수 있으므로)
            if (Vector3.Distance(owner.transform.position, target.position) <= data.attackRange + 1.0f)
            {
                // TODO: 플레이어 데미지 처리
                // target.GetComponent<PlayerHealth>().TakeDamage(data.attackDamage);
                Debug.Log($"[퍼펙트 타격] {target.name}에게 {data.attackDamage} 피해 발생");
            }
            else
            {
                Debug.Log("플레이어가 공격을 피했습니다");
            }
        }
    }
}