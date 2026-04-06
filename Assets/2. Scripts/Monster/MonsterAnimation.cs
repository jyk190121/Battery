using UnityEngine;

public class MonsterAnimation : MonoBehaviour
{
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
}