using UnityEngine;

public class AttackState : MonsterBaseState
{
    private float attackTimer;

    public AttackState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.isStopped = true; // 공격 시 멈춤
        attackTimer = data.attackCooldown; // 즉시 공격 가능하게 설정하거나 대기
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

        // 공격 쿨타임 체크
        if (attackTimer >= data.attackCooldown)
        {
            PerformAttack(target);
            attackTimer = 0f;
        }
    }

    private void PerformAttack(Transform target)
    {
        // 1. 애니메이션 실행 (애니메이션 핸들러 호출)
        owner.animHandler.PlayAttack();

        // 2. 실제 데미지 판정 (서버에서 실행)
        // 팀원이 만든 플레이어 체력 스크립트를 호출해야 합니다.
        // 예: target.GetComponent<PlayerHealth>().TakeDamage(data.attackDamage);
        Debug.Log($"{target.name}에게 {data.attackDamage}의 데미지를 입혔습니다!");
    }
}