using UnityEngine;

public class MonsterAnimation : MonoBehaviour
{
    public MonsterController controller;
    private Animator animator;
    // 파라미터 해싱
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int AlertnessFloatHash = Animator.StringToHash("Alertness");

    public void UpdateAnimation(float speed, float alertness)
    {
        animator.SetBool(IsMovingHash, speed > 0.1f);
        animator.SetFloat(AlertnessFloatHash, alertness);
    }

    public void PlayAttack() => animator.SetTrigger(AttackTriggerHash);

    // 애니메이션 이벤트로 호출될 함수
    public void OnAnimationEvent_AttackHit()
    {
        if (controller != null && controller.IsServer)
        {
            controller.ExecuteAttackDamage(); // 본체에 데미지 실행 명령 전달
        }
    }
}