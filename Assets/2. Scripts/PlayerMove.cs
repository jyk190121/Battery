using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;                           // 코루틴용
using Key = UnityEngine.InputSystem.Key;


public class PlayerMove : MonoBehaviour
{
    Animator anim;

    [Header("이동 설정")]
    public float walkSpeed = 3.5f;                  // 기본 걷기 속도
    public float runSpeed = 6.0f;                   // 달리기 속도
    public float currentSpeed;                      // 현재 적용될 속도

    [Header("점프 설정")]
    public float jumpForce = 5.0f;                  // 점프 위력

    [Header("바닥 체크 설정")]
    public LayerMask groundLayer;                   // 인스펙터에서 Ground 레이어 선택
    public float groundCheckDistance = 0.25f;       // 바닥 감지 거리

    private bool isInputLocked = false;             // 이모션 중 입력 잠금 플래그
    Rigidbody rb;
    bool isGrounded = false;
    

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        currentSpeed = walkSpeed;

        // Rigidbody가 멋대로 회전해서 넘어지는 걸 방지
        rb.freezeRotation = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current == null) return;

        //if (isInputLocked) return;

        if (isInputLocked)
        {
            // 이동 애니메이션을 0으로 강제 고정 (미끄러짐 방지)
            anim.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
            return;
        }

        CheckGroundStatus(); // 매 프레임 바닥 상태 확인
        HandleMovement();
        HandleActions();

        //// 1. 현재 입력 축 값을 기반으로 실제 입력 강도(Magnitude)를 계산합니다.
        //float h = Input.GetAxisRaw("Horizontal");
        //float v = Input.GetAxisRaw("Vertical");

        //// 입력 벡터의 길이를 구합니다. (0 ~ 1 사이의 값)
        //float inputMagnitude = new Vector2(h, v).magnitude;

        //if (inputMagnitude > 0.1f)
        //{
        //    if (Keyboard.current.leftShiftKey.isPressed)
        //    {
        //        currentSpeed = runSpeed;
        //        // 세 번째 인자인 Damping 시간을 0으로 수정 (즉각 반영)
        //        anim.SetFloat("Speed", 2.0f, 0f, Time.deltaTime);
        //    }
        //    else
        //    {
        //        currentSpeed = walkSpeed;
        //        anim.SetFloat("Speed", 1.0f, 0f, Time.deltaTime);
        //    }
        //}
        //else
        //{
        //    // 멈출 때도 즉시 0으로
        //    anim.SetFloat("Speed", 0f, 0f, Time.deltaTime);
        //}

        //Move(h, v);

        //// 2. 고정된 moveSpeed 대신, 현재 입력 강도를 애니메이터에 전달합니다.
        //// 이렇게 하면 키를 뗄 때 inputMagnitude가 0이 되어 Idle로 돌아갑니다.
        //anim.SetFloat("Speed", inputMagnitude);

        //Move(h, v);


    }
    void CheckGroundStatus()
    {
        // 캐릭터 발밑으로 레이를 쏘아 바닥인지 확인
        // CapsuleCollider를 사용 중이라면 위치 보정이 필요할 수 있습니다.
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, groundLayer);

        // 애니메이터에 바닥 상태 전달 (Landing 애니메이션 전환용)
        anim.SetBool("IsGrounded", isGrounded);
    }
    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        float inputMagnitude = new Vector2(h, v).magnitude;

        if (inputMagnitude > 0.1f)
        {
            // 이동 중일 때만 쉬프트 체크
            if (Keyboard.current.leftShiftKey.isPressed)
            {
                currentSpeed = runSpeed;
                anim.SetFloat("Speed", 2.0f, 0.05f, Time.deltaTime); // 아주 약간의 댐핑을 주면 더 부드럽습니다.
            }
            else
            {
                currentSpeed = walkSpeed;
                anim.SetFloat("Speed", 1.0f, 0.05f, Time.deltaTime);
            }
            Move(h, v);
        }
        else
        {
            anim.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
        }
    }

    void HandleActions()
    {
        // 2. 점프 (Space)
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            anim.SetTrigger("Jump");
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }

        // 3. 이모션 (1번 키)
        if (Keyboard.current.digit1Key.wasPressedThisFrame && isGrounded)
        {
            anim.SetTrigger("Emotion");
            StartCoroutine(LockInputDuringEmotion());
        }
    }

    // 이모션 애니메이션 동안 이동을 막는 코루틴
    IEnumerator LockInputDuringEmotion()
    {
        isInputLocked = true;
        anim.SetFloat("Speed", 0f); // 이동 애니메이션 초기화

        // 이모션 중에는 물리 속도 멈춤
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        // 애니메이션 길이에 맞춰 대기 (예: 2초, 실제 애니메이션 길이에 맞춰 수정)
        yield return new WaitForSeconds(2.0f);

        currentSpeed = walkSpeed;
        isInputLocked = false;
    }


    void Move(float h, float v)
    {
        Vector3 moveDir = new Vector3(h, 0, v);

        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }

        //transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
        //transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.Self);

        //print(currentSpeed);

        Vector3 worldMoveDir = transform.TransformDirection(moveDir);
        transform.position += worldMoveDir * currentSpeed * Time.deltaTime;
    }

    // 레이캐스트 시각화 (디버깅용)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * groundCheckDistance);
    }
}
