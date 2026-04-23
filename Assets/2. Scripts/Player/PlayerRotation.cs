using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerRotation : NetworkBehaviour
{

    [Header("참조")]
    public CinemachineCamera vcam;          // 인스펙터에서 시네머신 카메라 할당
    public Transform cameraTarget;          // eye_Cinemachine 오브젝트를 여기에 할당
    PlayerMove playerMove;                  // 이동 속도를 체크하기 위해 참조
    PlayerController playerController;      // 플레이어 상태에 따라 로테이션처리를 하기위한 참조
    public GameObject CameraGroup;          // 휴대폰 촬영용 카메라도 같이 회전 처리

    [Header("카메라 위치 제어")]
    public float originYoffset = 0.0961f;
    public float walkYPos = 0.1f;           // 평소(걷기) Z 위치
    public float runYPos = 0.4f;            // 달리기 시 Z 위치 (입안이 안 보이게 앞으로 밀기)
    public float transitionSpeed = 10f;     // 위치 전환 부드러움 정도
    public float crouchZPos = 0f;           // 앉았을 때 눈높이 (수치 최적화 필요)

    [Header("카메라 전방 거리(Z축) 제어")]
    public float walkZPos = 0.15f;          // 평소 앞뒤 위치
    public float crouchYPos = 0.6f;         // 앉았을 때 카메라 위치 앞으로 조정

    [Header("회전 설정")]
    public float sensitivity = 0.1f;
    private bool _isTabletOpen = false;

    [Header("벽 뚫기 방지 설정")]
    public LayerMask collisionLayers;       // 벽(Default), 몬스터(Enemy) 레이어 선택
    public float minDistance = 0.1f;        // 최소 유지 거리
    public float collisionRadius = 0.2f;    // 충돌 감지 반경

    [Header("관전 상태")]
    bool _isSpectating = false;
    PlayerRotation _spectatingTarget;

    [Header("상태 확인")]
    public bool isHoldingSmartphone = false;

    private CinemachinePanTilt _panTilt;

    // 1. 상하 회전값 동기화를 위한 변수 (서버 권한)
    public NetworkVariable<float> NetVerticalRotation 
        = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        playerMove = GetComponent<PlayerMove>();
        playerController = GetComponent<PlayerController>();

        if (playerMove == null || playerController == null) { print("플레이어의 무브나 컨트롤이 없음");  return;  }

        if (IsOwner)
        {
            // [추가] 초기 커서 상태 설정: 태블릿이 꺼진 상태로 시작하므로 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;
            TryFindCamera();
        }
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
        _isTabletOpen = isOpen; // 태블릿 열림 상태 저장

        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
    }

    private void TryFindCamera()
    {

        if (vcam != null) return;

        vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            _panTilt = vcam.GetComponent<CinemachinePanTilt>();
            if (IsOwner)
            {
                vcam.Follow = cameraTarget;
                vcam.LookAt = null;
                if (_panTilt != null) _panTilt.PanAxis.Value = transform.eulerAngles.y;
            }
        }

        //if (vcam == null)
        //{
        //    vcam = FindAnyObjectByType<CinemachineCamera>();
        //    if (vcam != null)
        //    {
        //        _panTilt = vcam.GetComponent<CinemachinePanTilt>();
        //        // 내 카메라라면 타겟 설정
        //        if (IsOwner)
        //        {
        //            vcam.Follow = cameraTarget;
        //            vcam.LookAt = null;

        //            if (_panTilt != null)
        //            {
        //                _panTilt.PanAxis.Value = transform.eulerAngles.y;
        //            }
        //        }
        //    }
        //}
    }

    void Update()
    {
        if (!IsOwner) return; // 기본적으로 주인만 카메라를 제어

        //if (IsOwner)
        //{
        //    if (_isSpectating)
        //    {
        //        HandleSpectatingLogic();
        //        return;
        //    }

        //    // 정상적인 내 캐릭터 회전 처리
        //    HandleRotation();

        //    // 내 회전값을 다른 사람(나를 관전하는 사람)들이 알 수 있게 갱신
        //    if (_panTilt != null) NetVerticalRotation.Value = _panTilt.TiltAxis.Value;
        //}
        //// 2. 다른 플레이어의 오브젝트일 경우 (!IsOwner)
        //else
        //{
        //    // 내가 남을 관전 중인데, 이 오브젝트가 관전 대상이라면 대상의 회전값을 내 카메라에 강제 적용
        //    if (_isSpectating && _spectatingTarget != null)
        //    {
        //        ApplyTargetRotation();
        //    }
        //}

        if (_isSpectating)
        {
            // 관전 로직: 타겟의 회전값을 내 카메라에 복사
            HandleSpectatingLogic();
            return;
        }

        // 정상 상태: 내 마우스 입력 처리
        HandleRotation();

        // 내 상하 회전값 동기화 (관전하는 남들을 위해)
        if (_panTilt != null) NetVerticalRotation.Value = _panTilt.TiltAxis.Value;
    }

    void HandleRotation()
    {
        if (vcam == null || _panTilt == null)
        {
            TryFindCamera();
            if (vcam == null) return;
        }

        // 태블릿이 열려있으면 시선 이동 막기
        if (_isTabletOpen) return;

        // 1. 마우스 입력 데이터 가져오기
        Vector2 mouseDelta = Input.GetMouseDelta();

        // 2. 좌우 회전 (Pan) 업데이트
        _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

        bool isSnared = playerController != null && playerController.isSnared.Value;
        if (isSnared)
        {
            // 몬스터가 얼굴에 붙었을 때: 정면 혹은 약간 아래로 시선 고정 (예: 0도 또는 -5도)
            _panTilt.TiltAxis.Value = 0f;
        }

        // 3. 상하 회전 (Tilt) 업데이트
        //float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
        //_panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

        // 4. 상황별 회전 적용
        if (playerController.isDead.Value)
        {
            ApplyDeathRotation();
        }
        else if (playerMove != null && playerMove.IsCrouching)
        {
            // 앉아 있을 때: 기존 의도대로 -10도 고정
            _panTilt.TiltAxis.Value = -10f;
        }
        else
        {
            //// 정상 상태: 마우스 입력에 따라 상하 회전
            //float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
            //_panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

            isHoldingSmartphone = PhoneUIController.Instance.isPhoneActive;

            // [수정 포인트] 스마트폰 사용 여부에 따라 상하 각도 제한 변경
            float minTilt = -70f;
            float maxTilt = isHoldingSmartphone ? 5f : 70f; // 스마트폰 들고 있으면 5도까지만 아래를 봄

            float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
            _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, minTilt, maxTilt);

        }


        if (playerController.isDead.Value)
        {
            ApplyDeathRotation();
        }
        else
        {
            ApplyLivingRotation();
        }
    }

    void ApplyTargetRotation()
    {
        if (_spectatingTarget == null) return;

        // 대상의 상하 회전값 가져오기
        float targetTilt = _spectatingTarget.NetVerticalRotation.Value;

        // 내 시네머신 카메라의 각도를 대상과 동기화
        if (vcam.TryGetComponent<CinemachinePanTilt>(out var myPanTilt))
        {
            myPanTilt.TiltAxis.Value = targetTilt;
        }
    }


    void UpdateCameraPositionWithCollision()
    {
        if (cameraTarget == null || playerMove == null || _panTilt == null) return;

        float currentTilt = _panTilt.TiltAxis.Value;

        // 목표로 하는 기본 위치(Z축) 결정
        bool isRunning = playerMove.currentSpeed > playerMove.walkSpeed + 0.1f;
        float targetZ = playerMove.IsCrouching ? 0.6f : (isRunning ? runYPos : walkZPos);
        //float targetZ = playerMove.IsCrouching ? 0.6f : (playerMove.currentSpeed > playerMove.walkSpeed + 0.1f ? runYPos : walkZPos);


        // 0(정면) ~ 1(바닥)
        float tiltOffsetFactor = Mathf.Clamp01(currentTilt / 70f);
        // 아래를 볼 때 카메라가 너무 파묻히지 않게 높이 보정
        float dynamicY = originYoffset + (tiltOffsetFactor * 0.2f);
        // 아래를 볼 때 몬스터가 잘 보이도록 거리 보정
        float dynamicZ = targetZ + (tiltOffsetFactor * 0.1f);


        // --- 벽 뚫기 방지 (Raycast) 로직 ---
        // 눈 위치(cameraTarget의 부모 위치 등)에서 시선 방향으로 레이를 쏩니다.
        //Vector3 origin = transform.position + Vector3.up * (originYoffset + (playerMove.IsCrouching ? 0.6f : 1.5f));
        Vector3 headOffset = Vector3.up * (originYoffset + (playerMove.IsCrouching ? 0.5f : 1.5f));
        Vector3 origin = transform.position + headOffset;
        Vector3 direction = cameraTarget.forward;

        // SphereCast를 사용하여 카메라가 물리적으로 들어갈 공간이 있는지 확인
        if (Physics.SphereCast(origin, collisionRadius, direction, out RaycastHit hit, dynamicZ, collisionLayers))
        {
            // 충돌이 발생하면 충돌 지점보다 약간 앞(minDistance)에 카메라 배치
            dynamicZ = Mathf.Max(0, hit.distance - minDistance);
        }

        // 부드럽게 위치 적용
        Vector3 targetLocalPos = new Vector3(0, dynamicY, dynamicZ);
        cameraTarget.localPosition = Vector3.Lerp(cameraTarget.localPosition, targetLocalPos, Time.deltaTime * transitionSpeed);
    }

    private void HandleSpectatingLogic()
    {
        if (_isSpectating && _spectatingTarget != null && _panTilt != null)
        {
            //_panTilt.PanAxis.Value = _spectatingTarget.GetCurrentPan();
            //_panTilt.TiltAxis.Value = _spectatingTarget.GetCurrentTilt();

            _panTilt.PanAxis.Value = _spectatingTarget.transform.eulerAngles.y;
            _panTilt.TiltAxis.Value = _spectatingTarget.NetVerticalRotation.Value;
            if (CameraGroup != null) CameraGroup.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);
        }
    }



    // 관전 모드 시 회전 막기
    //public void SetSpectatingMode(bool isSpectating)
    //{
    //    if (vcam == null) return;

    //    // 시네머신의 마우스 입력 컴포넌트 (POV 혹은 PanTilt)를 가져옵니다.
    //    var panTilt = vcam.GetComponent<CinemachinePanTilt>();

    //    if (isSpectating)
    //    {
    //        // 1. 마우스 입력 끄기
    //        if (panTilt != null) panTilt.enabled = false;

    //        // 2. 카메라의 로컬 회전값을 0으로 리셋 (부모인 cameraTarget과 일치하게 함)
    //        // 이렇게 해야 대상의 시야와 정확히 일치하는 정면을 봅니다.
    //        vcam.transform.localRotation = Quaternion.identity;
    //    }
    //    else
    //    {
    //        // 부활 시 다시 마우스 입력 켜기
    //        if (panTilt != null) panTilt.enabled = true;
    //    }
    //}


    #region 사망 시 로테이션 변경 점 처리 함수

    //public float GetCurrentPan() => _panTilt != null ? _panTilt.PanAxis.Value : transform.eulerAngles.y;
    //public float GetCurrentTilt() => _panTilt != null ? _panTilt.TiltAxis.Value : 0;

    //// 관전 시 대상의 회전값을 내 카메라에 강제 주입하는 함수
    //public void SyncRotation(float pan, float tilt)
    //{
    //    //// vcam을 찾지 못한 상태라면 찾기 시도
    //    //if (vcam == null) TryFindCamera();

    //    //if (_panTilt != null)
    //    //{
    //    //    // 대상의 회전값을 내 PanTilt 값에 직접 대입
    //    //    _panTilt.PanAxis.Value = pan;
    //    //    _panTilt.TiltAxis.Value = tilt;

    //    //    // 시각적으로 즉시 갱신하기 위해 CameraGroup과 본체 회전도 업데이트
    //    //    CameraGroup.transform.rotation = Quaternion.Euler(tilt, pan, 0);
    //    //    transform.rotation = Quaternion.Euler(0, pan, 0);
    //    //}

    //    if (vcam == null) TryFindCamera();

    //    if (_panTilt != null)
    //    {
    //        _panTilt.enabled = false;

    //        // 1. 입력을 즉시 멈추고 현재 상태값을 대상의 값으로 강제 교체
    //        _panTilt.PanAxis.Value = pan;
    //        _panTilt.TiltAxis.Value = tilt;

    //        // 2. 만약 카메라가 튀는 현상이 지속되면, 다음 프레임에 적용되도록 코루틴 사용
    //        //StartCoroutine(SyncRoutine(pan, tilt));
    //        if (vcam.Follow != null)
    //        {
    //            vcam.OnTargetObjectWarped(vcam.Follow, vcam.Follow.position - vcam.transform.position);
    //        }

    //        // 3. 수동 트랜스폼 갱신 (이미 처리 중인 부분)
    //        if (CameraGroup != null) CameraGroup.transform.rotation = Quaternion.Euler(tilt, pan, 0);

    //        transform.rotation = Quaternion.Euler(0, pan, 0);
    //    }
    //}

    ////IEnumerator SyncRoutine(float pan, float tilt)
    ////{
    ////    // 한 프레임 대기하여 시네머신이 위치를 다시 계산할 여유를 줌
    ////    yield return new WaitForSeconds(0.5f);

    ////    _panTilt.PanAxis.Value = pan;
    ////    _panTilt.TiltAxis.Value = tilt;

    ////    // 회전값 초기화
    ////    CameraGroup.transform.rotation = Quaternion.Euler(tilt, pan, 0);
    ////    transform.rotation = Quaternion.Euler(0, pan, 0);
    ////}

    //public void SetSpectatingTarget(PlayerRotation target)
    //{
    //    _spectatingTarget = target;
    //    _isSpectating = (target != null);

    //    if (vcam == null) TryFindCamera();

    //    if (_isSpectating)
    //    {
    //        // 관전 시에는 내 시네머신 입력(마우스)을 잠시 끕니다. 
    //        // (값이 튀는 것을 방지하고 오직 대상의 값만 받기 위함)
    //        if (_panTilt != null) _panTilt.enabled = false;
    //    }
    //    else
    //    {
    //        // 부활 시 다시 입력 활성화
    //        if (_panTilt != null) _panTilt.enabled = true;
    //    }
    //}

    public void SetSpectatingTarget(PlayerRotation target)
    {
        _spectatingTarget = target;
        _isSpectating = (target != null);

        if (vcam == null) TryFindCamera();

        //if (_panTilt != null) _panTilt.enabled = !_isSpectating;
        if (_isSpectating)
        {
            // 1. 관전 대상의 눈(Target) 위치만 추적하도록 설정
            vcam.Follow = target.cameraTarget;

            if (_panTilt != null) _panTilt.enabled = false;

            //// 2. 중요: 관전 시 Pan/Tilt 값을 초기화하거나 현재 값을 유지하여 
            //// 대상이 누워있더라도 내 카메라는 수평을 유지하게 함
            //if (vcam.TryGetComponent<CinemachinePanTilt>(out var panTilt))
            //{
            //    panTilt.enabled = true; // 내 마우스 입력을 다시 활성화
            //}
        }
        else
        {
            vcam.Follow = cameraTarget;
            if (_panTilt != null) _panTilt.enabled = true;
        }
    }

    public void SetSpectatingMode(bool isSpectating)
    {
        if (_panTilt != null) _panTilt.enabled = !isSpectating;
        //if (isSpectating) vcam.transform.localRotation = Quaternion.identity;
        if (isSpectating)
        {
            // 1. 시네머신 가상 카메라의 로컬 회전 완전 초기화 (Z축 포함)
            vcam.transform.localRotation = Quaternion.identity;

            // 2. 카메라 그룹(실제 카메라가 붙는 부모)의 회전 초기화
            if (CameraGroup != null)
            {
                // 부모가 애니메이션으로 인해 누워있을 수 있으므로, 
                // 로컬 회전을 초기화하여 월드 기준 똑바로 서게 만듭니다.
                CameraGroup.transform.localRotation = Quaternion.identity;
            }

            // 3. 만약 캔버스나 다른 UI가 틸트되어 있다면 추가 보정
            // (필요 시 vcam.Lens.Dutch = 0; 를 통해 시네머신 틸트 강제 초기화)
            vcam.Lens.Dutch = 0;
        }
    }

    void ApplyDeathRotation()
    {
        // 본체(transform) 회전은 건드리지 않음
        if (CameraGroup != null)
        {
            // 시선(CameraGroup)만 Pan/Tilt 적용
            CameraGroup.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);
        }

        // 사망 시에는 카메라 위치 보정(벽뚫방지 등)을 멈추거나 
        // 누워있는 시점에 맞게 최소화하는 것이 자연스럽습니다.
    }

    void ApplyLivingRotation()
    {
        transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);

        // 시선은 상하좌우(Pan/Tilt) 모두 회전
        if (CameraGroup != null)
        {
            CameraGroup.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);
        }

        UpdateCameraPositionWithCollision();
    }

    #endregion
}