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
    public float crouchSpeed = 1.5f;                // 추가: 앉았을 때 속도
    public float currentSpeed;                      // 현재 적용될 속도
    float inputMagnitude;                           // 실시간 입력

    [Header("점프 설정")]
    public float jumpForce = 5.0f;                  // 점프 위력

    [Header("바닥 체크 설정")]
    public LayerMask groundLayer;                   // 인스펙터에서 Ground 레이어 선택
    public float groundCheckDistance = 0.3f;       // 바닥 감지 거리

    [Header("계단 체크 설정")]
    public LayerMask stairLayer;                    // 인스펙터에서 Stair 레이어 선택
    bool isOnStair = false;                         // 현재 계단 위인지 여부
    bool wasOnStair = false;                        // 이전 프레임의 계단 상태 체크용

    [SerializeField] float stairStepUpForce = 8.0f; // 계단 진입 시 살짝 띄게
    [SerializeField] float stairDownForce = 0.1f;    // 계단 내려갈때 중력

    [Header("계단 디테일 설정")]
    public float stepHeight = 0.35f;                 // 올라갈 수 있는 최대 계단 높이
    public float stepSmoothing = 0.1f;               // 올라가는 부드러움 정도

    [Header("콜라이더 설정")]
    private CapsuleCollider col;
    bool lastCrouchState = false;
    private float initialHeight;
    private Vector3 initialCenter;

    [SerializeField] float crouchHeight = 1.2f; // 앉았을 때 높이 (캐릭터에 맞춰 조절)
    [SerializeField] Vector3 crouchCenter = new Vector3(0, 0.6f, 0); // 앉았을 때 중심점

    // [Header("앉기 체크 설정")]
    bool isCrouching = false;

    public bool IsCrouching => isCrouching;

    Rigidbody rb;
    bool isGrounded = false;

    PlayerAnim playerAnim;
    PlayerStateManager stateManager;

    void Start()
    {
        col = GetComponent<CapsuleCollider>();

        initialHeight = col.height;
        initialCenter = col.center;

        rb = GetComponent<Rigidbody>();
        //anim = GetComponent<Animator>();
        playerAnim = GetComponent<PlayerAnim>();
        stateManager = GetComponent<PlayerStateManager>();

        currentSpeed = walkSpeed;

        // Rigidbody가 멋대로 회전해서 넘어지는 걸 방지
        //rb.freezeRotation = true;

        //rb.interpolation = RigidbodyInterpolation.Interpolate;
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
        HandleActions();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        // 이동 핸들링을 FixedUpdate로 이동
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        UpdateCollider();
        HandleMovement(h, v);
    }

    void CheckGroundStatus()
    {
        RaycastHit hit;

        // 이전프레임 계단 저장 [추가]
        wasOnStair = isOnStair;
        //    // 바닥 체크 레이를 조금 더 길게 쏩니다 (내려가는 계단 감지용)
        //    float checkDist = groundCheckDistance;
        //    // 계단 위일 때는 레이를 2배 길게 쏴서 다음 칸을 미리 찾음
        //    if (wasOnStair) checkDist *= 2.0f; 

        //    // 캐릭터 발밑으로 레이를 쏘아 바닥인지 확인
        //    // CapsuleCollider를 사용 중이라면 위치 보정이 필요할 수 있습니다.
        //    //isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, groundLayer);
        //    isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, groundCheckDistance, groundLayer);

        //    // 애니메이터에 바닥 상태 전달 (Landing 애니메이션 전환용)
        //    //anim.SetBool("IsGrounded", isGrounded);

        //    //playerAnim.UpdateGroundStatus(isGrounded);

        //    if (isGrounded)
        //    {
        //        isOnStair = (stairLayer.value & (1 << hit.collider.gameObject.layer)) > 0;

        //        // 이전에 계단이 아니었으나 지금 계단이라면 [추가]
        //        // 올라가는 순간 보정 (진입 시)
        //        if (isOnStair && !wasOnStair)
        //        {
        //            transform.position += Vector3.up * stairStepUpForce;
        //        }

        //        // 내려가는 중 보정
        //        // 계단 위에서 이동 중일 때, 캐릭터가 공중에 뜨지 않게 아래로 밀어줌
        //        if (isOnStair && inputMagnitude > 0.1f)
        //        {
        //            // 리지드바디에 아래 방향으로 지속적인 힘을 가함 (Velocity 조절)
        //            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -stairDownForce, rb.linearVelocity.z);
        //        }
        //    }
        //    else
        //    {
        //        isOnStair = false;
        //    }

        //    // 애니메이터에 상태 전달
        //    playerAnim.UpdateGroundStatus(isGrounded);
        //    playerAnim.UpdateStairStatus(isOnStair);
        //}
        //void HandleMovement(float h, float v)
        //{
        //    if (isCrouching)
        //    {
        //        playerAnim.UpdateMoveAnimation(0f);
        //        return;
        //    }


        //    if (inputMagnitude > 0.1f && !isCrouching)
        //    {
        //        //playerAnim.UpdateStairStatus(isOnStair);

        //        // 계단 이용 시 0.8배율로 속도 감소
        //        float moveSpeedMultiplier = isOnStair ? 0.8f : 1.0f;

        //        // 스테미너 값에 따른 달리기 여부
        //        bool canRun = Keyboard.current.leftShiftKey.isPressed && isGrounded && !stateManager.IsExhausted;

        //        // 이동 중일 때만 쉬프트 체크
        //        if (canRun)
        //        {
        //            currentSpeed = runSpeed * moveSpeedMultiplier;
        //            playerAnim.UpdateMoveAnimation(2.0f);
        //            //currentSpeed = runSpeed;
        //            //anim.SetFloat("Speed", 2.0f, 0.05f, Time.deltaTime); // 아주 약간의 댐핑을 주면 더 부드럽습니다.
        //            //playerAnim.UpdateMoveAnimation(2.0f);
        //        }
        //        else if(isGrounded)
        //        {
        //            currentSpeed = walkSpeed * moveSpeedMultiplier;
        //            //anim.SetFloat("Speed", 1.0f, 0.05f, Time.deltaTime);
        //            playerAnim.UpdateMoveAnimation(1.0f);
        //        }
        //        Move(h, v);
        //    }
        //    else
        //    {
        //        //anim.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
        //        playerAnim.UpdateMoveAnimation(0f);
        //        playerAnim.UpdateStairStatus(false);
        //    }
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        float rayDistance = 0.1f + groundCheckDistance;
        if (wasOnStair) rayDistance *= 1.3f;

        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, out hit, rayDistance, groundLayer | stairLayer);

        if (isGrounded)
        {
            isOnStair = ((1 << hit.collider.gameObject.layer) & stairLayer) != 0;

            if (isOnStair && inputMagnitude < 0.1f)
            {
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;
            }

            // --- [하강 보정 수정] ---
            // 캐릭터가 올라가려는 의지가 없을 때(Y속도가 낮은 상태)만 DownForce 적용
            if (isOnStair && inputMagnitude > 0.1f)
            {
                // 수직 속도가 거의 없거나 아래쪽일 때만 밀착 (올라가는 중에는 방해 금지)
                if (rb.linearVelocity.y <= 0.1f)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, -stairDownForce, rb.linearVelocity.z);
                }
            }

        }
        else
        {
            isOnStair = false;
            rb.isKinematic = false;
        }

        playerAnim.UpdateGroundStatus(isGrounded);
        playerAnim.UpdateStairStatus(isOnStair);

    }

    void HandleMovement(float h, float v)
    {
        //if (isCrouching) return;

        if (inputMagnitude > 0.1f)
        {
            float moveSpeedMultiplier = isOnStair ? 0.85f : 1.0f;

            if (isCrouching)
            {
                currentSpeed = crouchSpeed; // 1.5f 적용
            }
            else
            {
                bool canRun = Keyboard.current.leftShiftKey.isPressed && isGrounded && !stateManager.IsExhausted;
                currentSpeed = canRun ? runSpeed : walkSpeed;
            }

            //bool canRun = Keyboard.current.leftShiftKey.isPressed && isGrounded && !stateManager.IsExhausted;

            //currentSpeed = (canRun ? runSpeed : walkSpeed) * moveSpeedMultiplier;

            currentSpeed *= moveSpeedMultiplier;
            bool isRunning = Keyboard.current.leftShiftKey.isPressed && !isCrouching && isGrounded && !stateManager.IsExhausted;
            playerAnim.UpdateMoveAnimation(isRunning ? 2.0f : 1.0f);

            Move(h, v);
        }
        else
        {
            playerAnim.UpdateMoveAnimation(0f);
            if (isGrounded && !isOnStair) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    void HandleActions()
    {
        isCrouching = Keyboard.current.leftCtrlKey.isPressed && isGrounded;

        //playerAnim.UpdateCrouchStatus(isCrouching);

       playerAnim.UpdateCrouchStatus(isCrouching);


        if (isCrouching) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            rb.isKinematic = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            playerAnim.PlayJump();
            playerAnim.StopEmotions();
        }

        if (isGrounded && inputMagnitude < 0.1f)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) playerAnim.PlayEmotion1();
            if (Keyboard.current.digit2Key.wasPressedThisFrame) playerAnim.PlayEmotion2();
        }
        else if (inputMagnitude > 0.1f)
        {
            playerAnim.StopEmotions();
        }
    }

    void Move(float h, float v)
    {
        //Vector3 moveDir = new Vector3(h, 0, v);

        //if (moveDir.sqrMagnitude > 1f)
        //{
        //    moveDir.Normalize();
        //}

        ////transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
        ////transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.Self);

        ////print(currentSpeed);

        //Vector3 worldMoveDir = transform.TransformDirection(moveDir);
        //Vector3 targetVelocity = worldMoveDir * currentSpeed;

        //// [개선]
        //if (isOnStair && inputMagnitude > 0.1f)
        //{
        //    RaycastHit hitStep;
        //    // 레이 높이를 조금 더 세밀하게 조정 (stepHeight의 절반 정도)
        //    Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

        //    if (Physics.Raycast(rayOrigin, worldMoveDir, out hitStep, 0.5f, stairLayer))
        //    {
        //        // 직접 position을 더하기보다 Y축 속도를 부드럽게 제어
        //        float smoothY = Mathf.Lerp(rb.linearVelocity.y, stepHeight * currentSpeed, stepSmoothing);
        //        rb.linearVelocity = new Vector3(rb.linearVelocity.x, smoothY, rb.linearVelocity.z);
        //    }
        //}

        ////transform.position += worldMoveDir * currentSpeed * Time.deltaTime;
        //Vector3 nextPos = rb.position + worldMoveDir * currentSpeed * Time.fixedDeltaTime;
        //rb.MovePosition(nextPos);

        Vector3 moveDir = new Vector3(h, 0, v);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 worldMoveDir = transform.TransformDirection(moveDir);
        //bool isStepUpDetected = false;

        // [계단 오르기 감지 및 처리]
        if (inputMagnitude > 0.1f)
        {
            RaycastHit hitLower;
            Vector3 lowerOrigin = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(lowerOrigin, worldMoveDir, out hitLower, 0.4f, stairLayer))
            {
                RaycastHit hitUpper;
                Vector3 upperOrigin = transform.position + Vector3.up * stepHeight;

                if (!Physics.Raycast(upperOrigin, worldMoveDir, out hitUpper, 0.5f, stairLayer))
                {
                    // 오르기 상태 감지
                    //isStepUpDetected = true;
                    rb.isKinematic = false;

                    // 수직으로 밀어 올려 턱을 넘김
                    rb.position += Vector3.up * stairStepUpForce * Time.fixedDeltaTime;

                    // 오를 때는 하강 관성을 제거하여 부드럽게 상승하게 함
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                }
            }
        }

        // 이동 실행
        if (!rb.isKinematic)
        {
            Vector3 nextPos = rb.position + worldMoveDir * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(nextPos);
        }
    }

    // 외부에서 이동 여부를 확인하기 위한 프로퍼티
    public bool IsMoving => inputMagnitude > 0.1f;

    // 레이캐스트 시각화 (디버깅용)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * (0.1f + groundCheckDistance));

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 0.5f);
        Gizmos.DrawRay(transform.position + Vector3.up * stepHeight, transform.forward * 0.6f);
    }

    void UpdateCollider()
    {
        if (isCrouching != lastCrouchState)
        {
            if (isCrouching)
            {
                col.height = crouchHeight;
                // 캡슐의 밑면을 발바닥(0)에 맞추는 계산식
                col.center = new Vector3(initialCenter.x, crouchHeight / 2f, initialCenter.z);
            }
            else
            {
                col.height = initialHeight;
                // 서 있을 때도 마찬가지로 높이의 절반을 센터로 잡음
                col.center = new Vector3(initialCenter.x, initialHeight / 2f, initialCenter.z);
            }

            lastCrouchState = isCrouching;

            // 보정 후 물리 엔진이 즉시 인지하도록 강제 업데이트
            rb.WakeUp();
        }
    }
}