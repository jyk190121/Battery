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
    bool wasOnStair = false;                        // 이전 프레임의 계단 상태 체크용

    [SerializeField] float stairStepUpForce = 0.3f; // 계단 진입 시 살짝 띄게
    [SerializeField] float stairDownForce = 10f;    // 계단 내려갈때 중력

    [Header("계단 디테일 설정")]
    public float stepHeight = 0.3f;                 // 올라갈 수 있는 최대 계단 높이
    public float stepSmoothing = 0.1f;              // 올라가는 부드러움 정도

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
        //HandleMovement(h, v);
        HandleActions();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        // 이동 핸들링을 FixedUpdate로 이동
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        HandleMovement(h, v);
    }

    void CheckGroundStatus()
    {
        RaycastHit hit;

        // 이전프레임 계단 저장 [추가]
        wasOnStair = isOnStair;
        // 바닥 체크 레이를 조금 더 길게 쏩니다 (내려가는 계단 감지용)
        float checkDist = groundCheckDistance;
        // 계단 위일 때는 레이를 2배 길게 쏴서 다음 칸을 미리 찾음
        if (wasOnStair) checkDist *= 2.0f; 

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

            // 이전에 계단이 아니었으나 지금 계단이라면 [추가]
            // 올라가는 순간 보정 (진입 시)
            if (isOnStair && !wasOnStair)
            {
                transform.position += Vector3.up * stairStepUpForce;
            }

            // 내려가는 중 보정
            // 계단 위에서 이동 중일 때, 캐릭터가 공중에 뜨지 않게 아래로 밀어줌
            if (isOnStair && inputMagnitude > 0.1f)
            {
                // 리지드바디에 아래 방향으로 지속적인 힘을 가함 (Velocity 조절)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -stairDownForce, rb.linearVelocity.z);
            }
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
            //playerAnim.UpdateStairStatus(isOnStair);

            // 계단 이용 시 0.8배율로 속도 감소
            float moveSpeedMultiplier = isOnStair ? 0.8f : 1.0f;

            // 스테미너 값에 따른 달리기 여부
            bool canRun = Keyboard.current.leftShiftKey.isPressed && isGrounded && !stateManager.IsExhausted;

            // 이동 중일 때만 쉬프트 체크
            if (canRun)
            {
                currentSpeed = runSpeed * moveSpeedMultiplier;
                playerAnim.UpdateMoveAnimation(2.0f);
                //currentSpeed = runSpeed;
                //anim.SetFloat("Speed", 2.0f, 0.05f, Time.deltaTime); // 아주 약간의 댐핑을 주면 더 부드럽습니다.
                //playerAnim.UpdateMoveAnimation(2.0f);
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
            playerAnim.UpdateStairStatus(false);
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
        Vector3 targetVelocity = worldMoveDir * currentSpeed;

        // [개선]
        if (isOnStair && inputMagnitude > 0.1f)
        {
            RaycastHit hitStep;
            // 레이 높이를 조금 더 세밀하게 조정 (stepHeight의 절반 정도)
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(rayOrigin, worldMoveDir, out hitStep, 0.5f, stairLayer))
            {
                // 직접 position을 더하기보다 Y축 속도를 부드럽게 제어
                float smoothY = Mathf.Lerp(rb.linearVelocity.y, stepHeight * currentSpeed, stepSmoothing);
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, smoothY, rb.linearVelocity.z);
            }
        }

        //transform.position += worldMoveDir * currentSpeed * Time.deltaTime;
        Vector3 nextPos = rb.position + worldMoveDir * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPos);
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
