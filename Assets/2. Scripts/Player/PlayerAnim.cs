using UnityEngine;

public class PlayerAnim : MonoBehaviour
{
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    // 이동 애니메이션 업데이트 (Speed 파라미터)
    public void UpdateMoveAnimation(float speed)
    {
        // 0.05f 댐핑을 주어 걷기/달리기 전환을 부드럽게 합니다.
        anim.SetFloat("Speed", speed, 0.05f, Time.deltaTime);
    }

    // 바닥 상태 업데이트 (IsGrounded 파라미터)
    public void UpdateGroundStatus(bool isGrounded)
    {
        anim.SetBool("IsGrounded", isGrounded);
    }

    // 점프 트리거 실행
    public void PlayJump()
    {
        anim.SetTrigger("Jump");
    }

    // 이모션 트리거 실행
    // 첫 번째 이모션 (1번 키용)
    public void PlayEmotion1()
    {
        anim.SetBool("Emotion1", true);
        anim.SetBool("Emotion2", false);
    }

    // 두 번째 이모션 (2번 키용)
    public void PlayEmotion2()
    {
        anim.SetBool("Emotion1", false);
        anim.SetBool("Emotion2", true);
    }

    public void StopEmotions()
    {
        anim.SetBool("Emotion1", false);
        anim.SetBool("Emotion2", false);
    }
}