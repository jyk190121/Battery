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
        anim.SetFloat("Speed", speed, 0f, Time.deltaTime);
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

    // 앉을 때 체크용
    public void UpdateCrouchStatus(bool isCrouching)
    {
        anim.SetBool("IsCrouching", isCrouching);
    }

    public void UpdateStairStatus(bool isStair)
    {
        anim.SetBool("IsStair", isStair);
    }

    // 손 올리기 동작
    //public void SetLayerWeight(string layerName, float weight)
    //{
    //    if (anim == null) anim = GetComponent<Animator>();

    //    // 만약 여전히 null이라면 (Animator 컴포넌트가 없을 때) 리턴하여 에러 방지
    //    if (anim == null) return;

    //    int layerIndex = anim.GetLayerIndex(layerName);
    //    if (layerIndex != -1)
    //    {
    //        anim.SetLayerWeight(layerIndex, weight);
    //    }
    //    else
    //    {
    //        Debug.LogWarning($"{layerName} 레이어를 찾을 수 없습니다. Animator 이름을 확인하세요.");
    //    }
    //}

    public void UpdatePhoneAnimation(bool isUsing)
    {
        if (anim == null) return;

        // 1. 애니메이터의 파라미터 값을 변경 (Transition이 발생하도록)
        anim.SetBool("isUsingPhone", isUsing);

        // 2. 레이어 가중치 조절 (레이어 이름이 정확히 "RightHand"여야 함)
        SetLayerWeight("RightHand", isUsing ? 1f : 0f);
    }

    public void SetLayerWeight(string layerName, float weight)
    {
        if (anim == null) return;

        int layerIndex = anim.GetLayerIndex(layerName);
        if (layerIndex != -1)
        {
            anim.SetLayerWeight(layerIndex, weight);
        }
    }

    public void PlayDead()
    {
        anim.SetTrigger("IsDead");
    }

    public void ResetAnimation()
    {
        anim.Rebind();
    }
}