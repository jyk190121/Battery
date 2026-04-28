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

    [SerializeField] float crouchHeight = 1.2f;                      // 앉았을 때 높이 (캐릭터에 맞춰 조절)

    // [Header("앉기 체크 설정")]
    bool isCrouching = false;

    public bool IsCrouching => isCrouching;

    Rigidbody rb;
    bool isGrounded = false;

    public bool IsGrounded => isGrounded;

    bool isControlLocked = false;

    bool isTabletLocked = false;

    PlayerAnim playerAnim;
    PlayerStateManager stateManager;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        initialHeight = col.height;
        initialCenter = col.center;

        //if (IsOwner)
        //{
        //    // 1. 태블릿 상태 변경 이벤트 구독
        //    TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;

        //    // 2. [중요] 초기화 시점에 태블릿이 열려있는지 확인 (싱글톤 혹은 정적 변수 참조 가능 시)
        //    // 만약 TabletUIManager에 정적 프로퍼티가 있다면 여기서 직접 할당
        //    // isTabletLocked = TabletUIManager.IsAnyTabletOpen; 
        //}

        TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 물리 상태 강제 해제
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            isControlLocked = false;
            isTabletLocked = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            TabletUIManager.OnTabletStateChanged -= HandleTabletStateChanged;
        }
    }


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

        if (isControlLocked || isTabletLocked)
        {
            inputMagnitude = 0;
            if (rb != null && isGrounded)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
            return;
        }

        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        //if (isControlLocked)
        //{
        //    // 애니메이션 파라미터 초기화용
        //    inputMagnitude = 0; 
        //    return;
        //}

        if (Keyboard.current == null) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        inputMagnitude = new Vector2(h, v).magnitude;

        //HandleInput();
        CheckGroundStatus();
        HandleActions();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        // [수정] 이동 물리 연산도 동일하게 차단
        if (isControlLocked || isTabletLocked)
        {
            // 완벽하게 멈추기 위해 물리 속도 제어
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        //Move();

        //if (!IsOwner) return;

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
       
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        float rayDistance = 0.1f + groundCheckDistance;
        if (wasOnStair) rayDistance *= 1.3f;

        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, out hit, rayDistance, groundLayer | stairLayer);

        if (isGrounded)
        {
            bool layerIsStair = ((1 << hit.collider.gameObject.layer) & stairLayer) != 0;

            float angle = Vector3.Angle(Vector3.up, hit.normal);
            bool isSloped = angle > 5f; // 5도 이상 기울어져 있어야 계단 애니메이션 발동
            isOnStair = layerIsStair && isSloped;
            //isOnStair = ((1 << hit.collider.gameObject.layer) & stairLayer) != 0;

            // 애니메이션 초기화 로직 추가 (Stair -> Ground로 레이어가 변경될 떄) 
            // 상태 변화 감지 및 애니메이션 강제 초기화
            if (wasOnStair && !isOnStair)
            {
                playerAnim.UpdateStairStatus(false);
                SyncMoveAnimation();

                // 물리 속도 보정 (계단에서의 하강 힘 제거)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            }

            if (isOnStair && inputMagnitude < 0.1f && !isTabletLocked)
            {
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;
            }

            //// --- [하강 보정 수정] ---
            //// 캐릭터가 올라가려는 의지가 없을 때(Y속도가 낮은 상태)만 DownForce 적용
            //if (isOnStair && inputMagnitude > 0.1f)
            //{
            //    // 수직 속도가 거의 없거나 아래쪽일 때만 밀착 (올라가는 중에는 방해 금지)
            //    if (rb.linearVelocity.y <= 0.1f)
            //    {
            //        rb.linearVelocity = new Vector3(rb.linearVelocity.x, -stairDownForce, rb.linearVelocity.z);
            //    }
            //}

        }
        else
        {
            isOnStair = false;
            rb.isKinematic = false;
        }

        playerAnim.UpdateGroundStatus(isGrounded);
        playerAnim.UpdateStairStatus(isOnStair);
    }

    // 애니메이션 동기화를 위한 헬퍼 함수
    void SyncMoveAnimation()
    {
        float animValue = 0f;
        if (inputMagnitude > 0.1f)
        {
            bool isRunning = Keyboard.current.leftShiftKey.isPressed && !isCrouching && !stateManager.IsExhausted;
            animValue = isRunning ? 2.0f : 1.0f;
        }
        playerAnim.UpdateMoveAnimation(animValue);
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
                // [수정 추천] 입력을 멈췄을 때의 처리
                playerAnim.UpdateMoveAnimation(0f);

                if (isGrounded && !isOnStair) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

                // 만약 정지했는데도 계단 애니메이션이 나오고 있다면 여기서 강제로 꺼줍니다.
                // (단, 계단 중간에 멈춰있어야 하는 기획이라면 이 줄은 제외하세요)
                if (!isOnStair) playerAnim.UpdateStairStatus(false);

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

    public void SetControlLock(bool isLocked)
    {
        isControlLocked = isLocked;

        // 공격 시작 시 즉시 속도 멈춤 처리
        if (isLocked)
        {
            currentSpeed = 0f;
            rb.linearVelocity = Vector3.zero;
        }
    }

    // 태블릿 상태에 따라 잠금 설정
    private void HandleTabletStateChanged(bool isOpen)
    {
        isTabletLocked = isOpen;

        // 태블릿이 열릴 때 속도 초기화 (미끄러짐 방지)
        if (isOpen)
        {
            currentSpeed = 0;
            inputMagnitude = 0;
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }
    }
}