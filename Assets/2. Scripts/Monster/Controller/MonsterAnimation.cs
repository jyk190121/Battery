using UnityEngine;

public class MonsterAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("이 애니메이션을 제어하는 마스터 컨트롤러")]
    public MonsterController controller;

    private Animator animator;

    [Header("Animation Settings")]
    [Tooltip("애니메이션 속도 전환의 부드러운 정도. 값이 클수록 즉각적으로 바뀝니다.")]
    public float animationBlendSpeed = 5f;

    // string 매칭 연산(GC 발생)을 피하기 위해 해시값으로 미리 변환하여 캐싱
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsSearchingHash = Animator.StringToHash("IsSearching");

    // 부드러운 속도 전환(Lerp)을 위한 내부 변수
    private float targetSpeed;
    private float currentSpeed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (animator == null) return;

        // 현재 속도와 목표 속도가 같으면 불필요한 연산을 스킵
        if (Mathf.Approximately(currentSpeed, targetSpeed)) return;

        // 목표 속도를 향해 부드럽게 보간 
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * animationBlendSpeed);

        // 오차가 아주 작아지면(0.01 이하) 강제 고정하여 Lerp 특유의 무한 소수점 연산을 방지
        if (Mathf.Abs(currentSpeed - targetSpeed) < 0.01f)
        {
            currentSpeed = targetSpeed;
        }

        // 연산이 끝난 최종 보간 값을 애니메이터에 전달
        animator.SetFloat(SpeedHash, currentSpeed);
    }

    /// <summary>
    /// 이동 속도를 설정합니다.
    /// </summary>
    public void SetVisualSpeed(float currentVelocity, float patrolSpeed, float chaseSpeed, MonsterStateType currentState)
    {
        float targetNormalizedValue = 0f;

        // 상태에 따라 애니메이션 보간 속도를 동적으로 조절
        if (currentState == MonsterStateType.Chase || currentState == MonsterStateType.Attack)
        {
            // 추격하거나 공격/정지할 때는 애니메이션이 즉각적으로 반응하도록 속도업
            animationBlendSpeed = 15f;
        }
        else
        {
            // 순찰, 수색 등 평상시에는 부드럽게 전환
            animationBlendSpeed = 5f;
        }

        if (currentVelocity < 0.05f)
        {
            targetNormalizedValue = 0f;
        }
        else if (currentState == MonsterStateType.Patrol || currentState == MonsterStateType.Search)
        {
            targetNormalizedValue = Mathf.Lerp(0f, 0.5f, currentVelocity / patrolSpeed);
        }
        else
        {
            float chaseRatio = currentVelocity / chaseSpeed;
            targetNormalizedValue = Mathf.Lerp(0.5f, 1.0f, chaseRatio);
        }

        targetSpeed = Mathf.Clamp01(targetNormalizedValue);
    }

    /// <summary>
    /// 공격 애니메이션을 실행합니다.
    /// </summary>
    public void PlayAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
    }

    /// <summary>
    /// 수색(두리번거림) 애니메이션 상태를 설정합니다.
    /// </summary>
    public void SetSearching(bool isSearching)
    {
        if (animator != null)
        {
            animator.SetBool(IsSearchingHash, isSearching);
        }
    }

    // 애니메이션 이벤트로 호출될 함수
    public void OnAnimationEvent_AttackHit()
    {
        if (controller != null && controller.IsServer)
        {
            controller.ExecuteAttackDamage(); // 본체에 데미지 실행 명령 전달
        }
    }

    // 공격 모션 취소 함수
    public void CancelAttack()
    {
        if (animator != null)
        {
            animator.ResetTrigger(AttackHash); // 남아있을지 모르는 트리거 큐 삭제

            animator.CrossFade("Locomotion", 0.1f);
        }
    }

    // 죽는 모션
    public void TriggerDeath()
    {
        if (animator != null)
        {
            // 애니메이터에 "Die" 파라미터가 Trigger로 설정되어 있다고 가정
            animator.SetTrigger("Die");
        }
    }
}