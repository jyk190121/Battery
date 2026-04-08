using UnityEngine;

public class MonsterAnimation : MonoBehaviour
{
    public MonsterController controller;
    private Animator animator;
    // 파라미터 해싱
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsSearchingHash = Animator.StringToHash("IsSearching");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        // GetComponentInChildren<Animator>() 
    }

    // 1. 이동 속도 업데이트 (Blend Tree 제어용)
    public void SetSpeed(float speed)
    {
        animator.SetFloat(SpeedHash, speed);
    }

    // 2. 공격 애니메이션 실행
    public void PlayAttack()
    {
        animator.SetTrigger(AttackHash);
    }

    // 3. 수색(두리번) 애니메이션 토글
    public void SetSearching(bool isSearching)
    {
        animator.SetBool(IsSearchingHash, isSearching);
    }

    // 애니메이션 이벤트로 호출될 함수
    public void OnAnimationEvent_AttackHit()
    {
        if (controller != null && controller.IsServer)
        {
            controller.ExecuteAttackDamage(); // 본체에 데미지 실행 명령 전달
        }
    }
}