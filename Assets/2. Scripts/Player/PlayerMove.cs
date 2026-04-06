using System.Collections;                          // 코루틴용
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Key = UnityEngine.InputSystem.Key;


public class PlayerMove : NetworkBehaviour
{
    [Header("이동 설정")]
    public float walkSpeed = 3.5f;                  // 기본 걷기 속도
    public float runSpeed = 6.0f;                   // 달리기 속도
    public float currentSpeed;                      // 현재 적용될 속도
    float inputMagnitude;                           // 실시간 입력

    [Header("점프 설정")]
    public float jumpForce = 5.0f;                  // 점프 위력

    [Header("바닥 체크 설정")]
    public LayerMask groundLayer;                   // 인스펙터에서 Ground 레이어 선택
    public float groundCheckDistance = 0.25f;       // 바닥 감지 거리

    [Header("계단 체크 설정")]
    public LayerMask stairLayer;                    // 인스펙터에서 Stair 레이어 선택
    bool isOnStair = false;                         // 현재 계단 위인지 여부

    // [Header("앉기 체크 설정")]
    bool isCrouching = false;

    Rigidbody rb;
    bool isGrounded = false;

    PlayerAnim playerAnim;
    PlayerStateManager stateManager;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        //anim = GetComponent<Animator>();
        playerAnim = GetComponent<PlayerAnim>();
        stateManager = GetComponent<PlayerStateManager>();

        currentSpeed = walkSpeed;
        
        // Rigidbody가 멋대로 회전해서 넘어지는 걸 방지
        rb.freezeRotation = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;

        if (Keyboard.current == null) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        inputMagnitude = new Vector2(h, v).magnitude;

        CheckGroundStatus();
        HandleMovement(h, v);
        HandleActions();
    }

    void CheckGroundStatus()
    {
        RaycastHit hit;
        // 캐릭터 발밑으로 레이를 쏘아 바닥인지 확인
        // CapsuleCollider를 사용 중이라면 위치 보정이 필요할 수 있습니다.
        //isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, groundLayer);
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, groundCheckDistance, groundLayer);

        // 애니메이터에 바닥 상태 전달 (Landing 애니메이션 전환용)
        //anim.SetBool("IsGrounded", isGrounded);

        //playerAnim.UpdateGroundStatus(isGrounded);

        if (isGrounded)
        {
            isOnStair = (stairLayer.value & (1 << hit.collider.gameObject.layer)) > 0;
        }
        else
        {
            isOnStair = false;
        }

        // 애니메이터에 상태 전달
        playerAnim.UpdateGroundStatus(isGrounded);
        playerAnim.UpdateStairStatus(isOnStair);
    }
    void HandleMovement(float h, float v)
    {
        if (isCrouching)
        {
            playerAnim.UpdateMoveAnimation(0f);
            return;
        }


        if (inputMagnitude > 0.1f && !isCrouching)
        {
            // [추가] 계단 위일 때는 이동 속도를 약간 조절하거나 전용 속도를 적용할 수 있습니다.
            float moveSpeedMultiplier = isOnStair ? 0.8f : 1.0f;

            // 스테미너 값에 따른 달리기 여부
            bool canRun = Keyboard.current.leftShiftKey.isPressed && isGrounded && !isOnStair && !stateManager.IsExhausted;

            // 이동 중일 때만 쉬프트 체크
            if (canRun)
            {
                currentSpeed = runSpeed;
                //anim.SetFloat("Speed", 2.0f, 0.05f, Time.deltaTime); // 아주 약간의 댐핑을 주면 더 부드럽습니다.
                playerAnim.UpdateMoveAnimation(2.0f);
            }
            else if(isGrounded)
            {
                currentSpeed = walkSpeed * moveSpeedMultiplier;
                //anim.SetFloat("Speed", 1.0f, 0.05f, Time.deltaTime);
                playerAnim.UpdateMoveAnimation(1.0f);
            }
            Move(h, v);
        }
        else
        {
            //anim.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
            playerAnim.UpdateMoveAnimation(0f);

        }
    }

    void HandleActions()
    {
        // 앉기 상태 체크 (LCtrl 누르고 있는 동안 true)
        isCrouching = Keyboard.current.leftCtrlKey.isPressed && isGrounded;

        // 애니메이터에 전달
        playerAnim.UpdateCrouchStatus(isCrouching);

        if (isCrouching)
        {
            playerAnim.StopEmotions();
            return;
        }

        // 점프 (Space) 
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            playerAnim.PlayJump();

            playerAnim.StopEmotions();
            return;
        }

        // 이모션 실행 (바닥 + 정지 상태)
        if (isGrounded && inputMagnitude < 0.1f)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) playerAnim.PlayEmotion1();
            if (Keyboard.current.digit2Key.wasPressedThisFrame) playerAnim.PlayEmotion2();
        }
        // 이동 중이면 이모션 Bool 강제 리셋 (애니메이션 캔슬용)
        else if (inputMagnitude > 0.1f)
        {
            playerAnim.StopEmotions();
        }
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

    // 외부에서 이동 여부를 확인하기 위한 프로퍼티
    public bool IsMoving => inputMagnitude > 0.1f;

    // 레이캐스트 시각화 (디버깅용)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * groundCheckDistance);
    }
}
