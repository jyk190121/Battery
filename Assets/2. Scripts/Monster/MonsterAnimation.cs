using UnityEngine;

public class MonsterAnimation : MonoBehaviour
{
    public MonsterController controller;
    private Animator animator;
    // 파라미터 해싱
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsSearchingHash = Animator.StringToHash("IsSearching");

    private float targetSpeed; // 목표 속도를 기억할 변수 (부드러운 전환을 위해)
    private float currentSpeed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        // GetComponentInChildren<Animator>() 
    }

    private void Update()
    {
        if (currentSpeed == targetSpeed) return;

        // 목표 속도를 향해 부드럽게 값 변경
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 5f);

        // 오차가 아주 작아지면(0.01 이하) 그냥 목표치로 강제 고정 (Lerp의 무한 연산 방지)
        if (Mathf.Abs(currentSpeed - targetSpeed) < 0.01f)
        {
            currentSpeed = targetSpeed;
        }

        // 연산이 끝난 값을 애니메이터에 전달
        animator.SetFloat(SpeedHash, currentSpeed);
    }

    // 1. 이동 속도 업데이트
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