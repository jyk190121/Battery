using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerRotation : NetworkBehaviour
{
    [Header("관전할 플레이어")]
    bool _isSpectating = false;
    PlayerRotation _spectatingTarget;

    [Header("참조")]
    public CinemachineCamera vcam;      // 인스펙터에서 시네머신 카메라 할당
    public Transform cameraTarget;      // eye_Cinemachine 오브젝트를 여기에 할당
    public PlayerMove playerMove;       // 이동 속도를 체크하기 위해 참조
    public GameObject CameraGroup;      // 휴대폰 촬영용 카메라도 같이 회전 처리

    [Header("설정")]
    public float sensitivity = 0.1f;

    [Header("카메라 위치 제어")]
    public float originYoffset = 0.0961f;
    public float walkYPos = 0.1f;           // 평소(걷기) Z 위치
    public float runYPos = 0.4f;            // 달리기 시 Z 위치 (입안이 안 보이게 앞으로 밀기)
    public float transitionSpeed = 10f;     // 위치 전환 부드러움 정도
    public float crouchZPos = 0f;           // 앉았을 때 눈높이 (수치 최적화 필요)

    [Header("카메라 전방 거리(Z축) 제어")]
    public float walkZPos = 0.1f;           // 평소 앞뒤 위치
    public float crouchYPos = 0.6f;         // 앉았을 때 카메라 위치 앞으로 조정

    private CinemachinePanTilt _panTilt;

    public override void OnNetworkSpawn()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }
        if (IsOwner)
        {
            TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;
        }
        TryFindCamera();
    }
    public override void OnNetworkDespawn()
    {
        // 메모리 누수 방지를 위해 파괴 시 반드시 구독 해제
        if (IsOwner)
        {
            TabletUIManager.OnTabletStateChanged -= HandleTabletStateChanged;
        }
        base.OnNetworkDespawn();
    }

    // 태블릿이 열리거나 닫힐 때 호출되는 함수
    private void HandleTabletStateChanged(bool isOpen)
    {
        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void TryFindCamera()
    {
        if (vcam == null)
        {
            vcam = FindAnyObjectByType<CinemachineCamera>();
            if (vcam != null)
            {
                _panTilt = vcam.GetComponent<CinemachinePanTilt>();
                // 내 카메라라면 타겟 설정
                if (IsOwner)
                {
                    vcam.Follow = cameraTarget;
                    vcam.LookAt = null;

                    if (_panTilt != null)
                    {
                        _panTilt.PanAxis.Value = transform.eulerAngles.y;
                    }
                }
            }
        }
    }

    void LateUpdate()
    {
        if (!IsOwner) return; // 내 캐릭터만 마우스 회전 처리

        // 만약 Spawn 시점에 못 찾았다면 여기서 다시 시도 (성능을 위해 null일 때만)
        if (vcam == null) TryFindCamera();
        if (_panTilt == null) return;

        // [추가] 사망 상태 확인 (PlayerController 참조)
        if (TryGetComponent<PlayerController>(out var pc) && pc.isDead.Value)
        {
            // 관전 중이라면 카메라 값만 업데이트하고 함수 종료 (내 몸 회전 방지)
            if (_isSpectating && _spectatingTarget != null)
            {
                _panTilt.PanAxis.Value = _spectatingTarget.GetCurrentPan();
                _panTilt.TiltAxis.Value = _spectatingTarget.GetCurrentTilt();
                if (CameraGroup != null)
                    CameraGroup.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);
            }
            return;
        }


        if (_isSpectating && _spectatingTarget != null)
        {
            // 관전 대상의 Pan/Tilt를 내 카메라에 실시간 복사
            // 대상의 마우스 포인터(시선)가 움직이는 대로 내 화면도 움직입니다.
            _panTilt.PanAxis.Value = _spectatingTarget.GetCurrentPan();
            _panTilt.TiltAxis.Value = _spectatingTarget.GetCurrentTilt();
        }
        else if (!_isSpectating)
        {
            // 관전 중이 아닐 때만 기존 마우스 조작 실행
            Vector2 mouseDelta = Input.GetMouseDelta();
            _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

            if (playerMove != null && !playerMove.IsCrouching)
            {
                float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
                _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);
            }
            else if (playerMove != null && playerMove.IsCrouching)
            {
                _panTilt.TiltAxis.Value = -10f;
            }
        }

        //Vector2 mouseDelta = Input.GetMouseDelta();

        //// 1. 좌우 회전 (Pan) - 언제나 가능
        //_panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

        //// 2. 상하 회전 (Tilt) - 앉아있을 때는 입력을 무시함
        //// PlayerMove의 isCrouching 변수가 private이라면 public bool IsCrouching => isCrouching; 등으로 공개되어 있어야 합니다.
        //if (playerMove != null && !playerMove.IsCrouching)
        //{
        //    float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
        //    _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

        //}
        //else if (playerMove != null && playerMove.IsCrouching)
        //{
        //    float crouchViewOffset = -10f;

        //    //// 앉았을 때 정면을 보게 강제하고 싶다면 아래 주석 해제 (부드럽게 정렬됨)
        //    //_panTilt.TiltAxis.Value = Mathf.Lerp(_panTilt.TiltAxis.Value, crouchViewOffset, Time.deltaTime * transitionSpeed);

        //    //if (Mathf.Abs(_panTilt.TiltAxis.Value) < 0.1f) _panTilt.TiltAxis.Value = crouchViewOffset;

        //    // Lerp 대신 직접 값을 대입해서 변화가 있는지 먼저 확인하세요.
        //    _panTilt.TiltAxis.Value = crouchViewOffset;
        //}
        transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);
        if (CameraGroup != null) CameraGroup.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);

        //// 3. 본체 회전 동기화
        //transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        if (cameraTarget == null || playerMove == null) return;

        bool isCrouching = playerMove.IsCrouching;
        bool isRunning = playerMove.currentSpeed > playerMove.walkSpeed + 0.1f;

        //float targetY = isRunning ? runYPos : walkYPos;

        float targetHeightX = walkYPos; // 기존의 YPos 수치를 X축(실제 높이)에 사용
        float targetForwardY = walkZPos; // 기존의 ZPos 수치를 Y축(실제 거리)에 사용

        // 타겟 Y값 결정 (우선순위: 앉기 > 달리기 > 걷기)
        //float targetY = walkYPos;
        //if (isCrouching) targetY = crouchYPos;
        //else if (isRunning) targetY = runYPos;


        if (isCrouching)
        {
            targetForwardY = crouchYPos; // 앉았을 때 앞으로 밀기
            targetHeightX = crouchZPos;
        }
        else if (isRunning)
        {
            targetForwardY = runYPos;
        }

        // 현재 로컬 위치 가져오기
        Vector3 currentPos = cameraTarget.localPosition;

        //float snapSpeed = isCrouching ? transitionSpeed * 2f : transitionSpeed;

        float newX = Mathf.Lerp(currentPos.x, targetHeightX, Time.deltaTime * transitionSpeed);

        // Y값만 부드럽게 보간(Lerp)
        float newY = Mathf.Lerp(currentPos.y, targetForwardY + originYoffset, Time.deltaTime * transitionSpeed);

        // 새로운 위치 적용
        cameraTarget.localPosition = new Vector3(newX, newY, currentPos.z);
        
        //if (playerMove.IsCrouching)
        //{
        //    // 예: 로컬 회전값을 (0, 0, 0) 혹은 정면을 보는 특정 값으로 고정
        //    // 축이 꼬여있으므로 아래 Euler 값을 조절하며 정면을 찾는 과정이 필요합니다.
        //    cameraTarget.localRotation = Quaternion.Euler(90, 90, 90);
        //}
        //else
        //{
        //    // 서 있을 때 기본 회전값 (필요하다면)
        //    cameraTarget.localRotation = Quaternion.Euler(0, 0, 0);
        //}
    }

    // 관전 모드 시 회전 막기
    public void SetSpectatingMode(bool isSpectating)
    {
        if (vcam == null) return;

        // 시네머신의 마우스 입력 컴포넌트 (POV 혹은 PanTilt)를 가져옵니다.
        var panTilt = vcam.GetComponent<CinemachinePanTilt>();

        if (isSpectating)
        {
            // 1. 마우스 입력 끄기
            if (panTilt != null) panTilt.enabled = false;

            // 2. 카메라의 로컬 회전값을 0으로 리셋 (부모인 cameraTarget과 일치하게 함)
            // 이렇게 해야 대상의 시야와 정확히 일치하는 정면을 봅니다.
            vcam.transform.localRotation = Quaternion.identity;
        }
        else
        {
            // 부활 시 다시 마우스 입력 켜기
            if (panTilt != null) panTilt.enabled = true;
        }
    }


    #region 사망 시 로테이션 변경 점 처리 함수

    public float GetCurrentPan() => _panTilt != null ? 0: transform.eulerAngles.y;
    public float GetCurrentTilt() => _panTilt != null ? 0 : 0;

    // 관전 시 대상의 회전값을 내 카메라에 강제 주입하는 함수
    public void SyncRotation(float pan, float tilt)
    {
        //// vcam을 찾지 못한 상태라면 찾기 시도
        //if (vcam == null) TryFindCamera();

        //if (_panTilt != null)
        //{
        //    // 대상의 회전값을 내 PanTilt 값에 직접 대입
        //    _panTilt.PanAxis.Value = pan;
        //    _panTilt.TiltAxis.Value = tilt;

        //    // 시각적으로 즉시 갱신하기 위해 CameraGroup과 본체 회전도 업데이트
        //    CameraGroup.transform.rotation = Quaternion.Euler(tilt, pan, 0);
        //    transform.rotation = Quaternion.Euler(0, pan, 0);
        //}

        if (vcam == null) TryFindCamera();

        if (_panTilt != null)
        {
            _panTilt.enabled = false;

            // 1. 입력을 즉시 멈추고 현재 상태값을 대상의 값으로 강제 교체
            _panTilt.PanAxis.Value = pan;
            _panTilt.TiltAxis.Value = tilt;

            // 2. 만약 카메라가 튀는 현상이 지속되면, 다음 프레임에 적용되도록 코루틴 사용
            //StartCoroutine(SyncRoutine(pan, tilt));
            if (vcam.Follow != null)
            {
                vcam.OnTargetObjectWarped(vcam.Follow, vcam.Follow.position - vcam.transform.position);
            }

            // 3. 수동 트랜스폼 갱신 (이미 처리 중인 부분)
            if (CameraGroup != null) CameraGroup.transform.rotation = Quaternion.Euler(tilt, pan, 0);

            transform.rotation = Quaternion.Euler(0, pan, 0);
        }
    }

    //IEnumerator SyncRoutine(float pan, float tilt)
    //{
    //    // 한 프레임 대기하여 시네머신이 위치를 다시 계산할 여유를 줌
    //    yield return new WaitForSeconds(0.5f);

    //    _panTilt.PanAxis.Value = pan;
    //    _panTilt.TiltAxis.Value = tilt;

    //    // 회전값 초기화
    //    CameraGroup.transform.rotation = Quaternion.Euler(tilt, pan, 0);
    //    transform.rotation = Quaternion.Euler(0, pan, 0);
    //}

    public void SetSpectatingTarget(PlayerRotation target)
    {
        _spectatingTarget = target;
        _isSpectating = (target != null);

        if (vcam == null) TryFindCamera();

        if (_isSpectating)
        {
            // 관전 시에는 내 시네머신 입력(마우스)을 잠시 끕니다. 
            // (값이 튀는 것을 방지하고 오직 대상의 값만 받기 위함)
            if (_panTilt != null) _panTilt.enabled = false;
        }
        else
        {
            // 부활 시 다시 입력 활성화
            if (_panTilt != null) _panTilt.enabled = true;
        }
    }

    #endregion

}