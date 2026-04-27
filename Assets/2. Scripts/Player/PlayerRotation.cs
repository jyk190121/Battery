using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerRotation : NetworkBehaviour
{

    [Header("감도 설정 (최대 5)")]
    [Range(0.1f, 5f)]
    public float mouseSensitivityMultiplier = 1.0f; // 이 값을 설정창에서 조절

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

    [Header("폰사용중 상태 확인")]
    public bool isHoldingSmartphone = false; // [추가] 스마트폰 사용 여부
    GameObject _phoneUIParent;

    private CinemachinePanTilt _panTilt;

    // 1. 상하 회전값 동기화를 위한 변수 (서버 권한)
    public NetworkVariable<float> NetVerticalRotation = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> NetHorizontalRotation = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        playerMove = GetComponent<PlayerMove>();
        playerController = GetComponent<PlayerController>();

        if (playerMove == null || playerController == null) { print("플레이어의 무브나 컨트롤이 없음");  return;  }

        //if (IsOwner)
        //{
        //    // [추가] 초기 커서 상태 설정: 태블릿이 꺼진 상태로 시작하므로 잠금
        //    Cursor.lockState = CursorLockMode.Locked;
        //    Cursor.visible = false;

        //    TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;
        //    TryFindCamera();
        //}
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;

            TryFindCamera();
            if (_panTilt != null)
            {
                NetHorizontalRotation.Value = _panTilt.PanAxis.Value;
                NetVerticalRotation.Value = _panTilt.TiltAxis.Value;
            }
        }

        // 관전자 입장에서 타겟의 회전값이 변할 때마다 즉시 반응하도록 이벤트 등록
        NetHorizontalRotation.OnValueChanged += (oldVal, newVal) =>
        {
            if (!IsOwner && _isSpectating && _spectatingTarget != null)
            {
                // 이벤트 기반으로 즉시 동기화 로직을 보강할 수 있습니다.
            }
        };

        // 관전자 입장에서 타겟의 회전값이 변할 때마다 즉시 반응
        NetHorizontalRotation.OnValueChanged += UpdateRotationFromNetwork;
        NetVerticalRotation.OnValueChanged += UpdateRotationFromNetwork;
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

    }

    void Update()
    {
        if (_isSpectating || !IsOwner) return;


        if (PhoneUIController.Instance != null)
        {
            // 처음 한 번만 참조를 가져옴
            if (_phoneUIParent == null)
            {
                _phoneUIParent = PhoneUIController.Instance.phoneUIParent;
            }

            // 실제 부모 오브젝트가 켜져 있을 때만 '들고 있다'고 판단
            if (_phoneUIParent != null)
            {
                isHoldingSmartphone = _phoneUIParent.activeInHierarchy;
            }
            else
            {
                isHoldingSmartphone = PhoneUIController.Instance.isPhoneActive;
            }
        }

        HandleRotation();

        // 동기화 변수에 값 갱신 (HandleRotation에서 Clamp된 최종값을 보냄)
        if (_panTilt != null)
        {
            NetVerticalRotation.Value = _panTilt.TiltAxis.Value;
            NetHorizontalRotation.Value = _panTilt.PanAxis.Value;
        }
    }


    void LateUpdate()
    {
        if (!IsOwner) return;

        // 1. 카메라 및 컴포넌트 체크
        if (vcam == null || _panTilt == null)
        {
            TryFindCamera();
            if (vcam == null || _panTilt == null) return;
        }

        // 관전 모드라면 시네머신이 직접 계산하게 두지 않고 강제 동기화 후 종료
        if (_isSpectating)
        {
            HandleSpectatingLogic();
            return;
        }

        if (_isTabletOpen) return;

        ProcessMouseInput();

        // 4순위: 상태별 트랜스폼 적용 (사망 vs 생존)
        if (playerController != null && playerController.isDead.Value)
        {
            // 내가 죽었을 때 내 카메라를 눕히는 로직
            ApplyDeathRotation();

            //// 사망 연출용 위치 이동
            //Vector3 deathCameraPos = transform.position + Vector3.up * 1.5f;
            //vcam.transform.position = Vector3.Lerp(vcam.transform.position, deathCameraPos, Time.deltaTime * transitionSpeed);
        }
        else
        {
            // 살아있을 때 정상 회전
            ApplyLivingRotation();
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
    void ProcessMouseInput()
    {
        Vector2 mouseDelta = Input.GetMouseDelta();
        float finalSensitivity = sensitivity * mouseSensitivityMultiplier;

        // 좌우 회전
        _panTilt.PanAxis.Value += mouseDelta.x * finalSensitivity;

        // 상하 회전 (Tilt) 제약
        if (playerController != null && playerController.isSnared.Value)
        {
            _panTilt.TiltAxis.Value = Mathf.Lerp(_panTilt.TiltAxis.Value, 0f, Time.deltaTime * transitionSpeed);
        }
        else if (playerMove != null && playerMove.IsCrouching)
        {
            _panTilt.TiltAxis.Value = -10f;
        }
        else
        {
            float minTilt = -70f;
            float _currentMaxTilt = 70f;
            //float maxTilt = isHoldingSmartphone ? 20f : 70f;
            //_panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, minTilt, maxTilt);

            float targetMaxTilt = isHoldingSmartphone ? 20f : 70f;
            float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * finalSensitivity);
            _currentMaxTilt = Mathf.Lerp(_currentMaxTilt, targetMaxTilt, Time.deltaTime * transitionSpeed);
            _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, minTilt, _currentMaxTilt);
        }
    }

    #region 사망 시 로테이션 변경 점 처리 함수
    void HandleSpectatingLogic()
    {
        if (_spectatingTarget == null || vcam == null) return;

        // 타겟의 눈 위치로 카메라 이동
        if (_spectatingTarget.cameraTarget != null)
        {
            vcam.transform.position = _spectatingTarget.cameraTarget.position;
        }

        // 타겟의 데이터 로드
        float targetPan = _spectatingTarget.NetHorizontalRotation.Value;
        float targetTilt = _spectatingTarget.NetVerticalRotation.Value;

        // 내 PanTilt 컴포넌트도 동기화하여 관전 해제 시 튀지 않게 함
        if (_panTilt != null)
        {
            _panTilt.PanAxis.Value = targetPan;
            _panTilt.TiltAxis.Value = targetTilt;
        }

        // 월드 회전 적용 (Euler의 세 번째 인자인 Z를 0으로 고정하는 것이 핵심)
        Quaternion targetRot = Quaternion.Euler(targetTilt, targetPan, 0f);

        vcam.ForceCameraPosition(vcam.transform.position, targetRot);
        vcam.transform.rotation = targetRot;

        if (CameraGroup != null) CameraGroup.transform.rotation = targetRot;

        //vcam.Lens.Dutch = 0; // 화면 기울기 완전 초기화
    }
    //void ApplyTargetRotation()
    //{
    //    if (_spectatingTarget == null) return;

    //    // 대상의 상하 회전값 가져오기
    //    float targetTilt = _spectatingTarget.NetVerticalRotation.Value;

    //    // 내 시네머신 카메라의 각도를 대상과 동기화
    //    if (vcam.TryGetComponent<CinemachinePanTilt>(out var myPanTilt))
    //    {
    //        myPanTilt.TiltAxis.Value = targetTilt;
    //        // 좌우 회전은 대상의 몸체(Transform)를 Follow하므로 자동으로 따라갑니다.
    //    }
    //}

    void HandleRotation()
    {
        if (vcam == null || _panTilt == null)
        {
            TryFindCamera();
            if (vcam == null) return;
        }

        if (_isTabletOpen) return;

        Vector2 mouseDelta = Input.GetMouseDelta();

        // 1. 좌우 회전 업데이트
        //_panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

        float finalSensitivity = sensitivity * mouseSensitivityMultiplier;
        _panTilt.PanAxis.Value += mouseDelta.x * finalSensitivity;

        // 2. 상하 회전(Tilt) 로직
        bool isSnared = playerController != null && playerController.isSnared.Value;

        if (isSnared)
        {
            _panTilt.TiltAxis.Value = 0f;
        }
        else if (playerMove != null && playerMove.IsCrouching)
        {
            _panTilt.TiltAxis.Value = -10f; // 앉아 있을 때는 고정
        }
        else
        {
            // [수정 포인트] 스마트폰 사용 여부에 따라 상하 각도 제한 변경
            float minTilt = -70f;
            float maxTilt = isHoldingSmartphone ? 0f : 70f; // 스마트폰 들고 있으면 0도까지만 아래를 봄

            float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * finalSensitivity);
            _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, minTilt, maxTilt);
        }

        // 3. 상황별 트랜스폼 적용
        if (playerController.isDead.Value)
        {
            ApplyDeathRotation();
        }
        else
        {
            ApplyLivingRotation();
        }
    }


    public void SetSpectatingTarget(PlayerRotation target)
    {
        _spectatingTarget = target;
        _isSpectating = (target != null);

        if (vcam == null) TryFindCamera();

        if (_isSpectating)
        {
            vcam.Follow = null;
            vcam.LookAt = null;

            // [추가] 내 PanTilt 컴포넌트가 활성화되어 있다면 값을 타겟과 일치시킴
            // 이렇게 해야 HandleSpectatingLogic()이 실행될 때 튀지 않습니다.
            if (_panTilt != null) _panTilt.enabled = false;

            // 타겟 설정 직후 즉시 1회 강제 동기화
            ForceSyncRotation(target.NetHorizontalRotation.Value, target.NetVerticalRotation.Value);

            // 즉시 로직 1회 강제 실행하여 위치/회전 고정
            //HandleSpectatingLogic();
        }
        else
        {
            vcam.Follow = cameraTarget;
            if (_panTilt != null) _panTilt.enabled = true;
        }
    }

    public void SetSpectatingMode(bool isSpectating)
    {

        if (vcam != null && _panTilt != null) _panTilt.enabled = !isSpectating;
        if (isSpectating)
        {
            if (vcam != null)
            {
                vcam.transform.SetParent(null);

                vcam.transform.localRotation = Quaternion.identity;
                vcam.Lens.Dutch = 0;
            }

            if (CameraGroup != null)
            {
                CameraGroup.transform.localRotation = Quaternion.identity;
            }
        }
    }

    void ApplyDeathRotation()
    {
        if (!IsOwner || vcam == null || _panTilt == null) return;

        // 1. 시네머신 내부 렌즈 기울기 초기화
        vcam.Lens.Dutch = 0;

        Quaternion targetWorldRotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);

        vcam.ForceCameraPosition(cameraTarget.position + Vector3.up * 0.1f, targetWorldRotation);
        vcam.transform.rotation = targetWorldRotation;

        if (CameraGroup != null)
        {
            CameraGroup.transform.rotation = targetWorldRotation;
        }
    }

    void ApplyLivingRotation()
    {
        // 몸체는 좌우(Pan)만 회전
        transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);

        // 시선은 상하좌우(Pan/Tilt) 모두 회전
        if (CameraGroup != null)
        {
            CameraGroup.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);
        }

        UpdateCameraPositionWithCollision();
    }
    public float GetCurrentPan()
    {
        if (_panTilt != null && _panTilt.enabled)
            return _panTilt.PanAxis.Value;

        // 몸이 누워있어도 수평 방향(Forward)만 추출하여 Y축 회전광 계산
        Vector3 forward = transform.forward;
        forward.y = 0;
        return Quaternion.LookRotation(forward).eulerAngles.y;
    }
    //public float GetCurrentTilt() => _panTilt != null ? _panTilt.TiltAxis.Value : 0f;
    void UpdateRotationFromNetwork(float oldVal, float newVal)
    {
        // 내가 관전 중이고, 관전 대상이 있다면 마우스 입력과 상관없이 화면 갱신
        if (!IsOwner && _isSpectating && _spectatingTarget != null)
        {
            HandleSpectatingLogic();
        }
    }

    // 현재 내가 관전하고 있는 타겟의 PlayerRotation을 반환
    public PlayerRotation GetSpectatingTarget()
    {
        return _spectatingTarget;
    }

    // 관전 중인지 여부를 외부에서 확인하기 위한 프로퍼티 (선택 사항)
    public bool IsSpectating => _isSpectating;
    // 외부에서 관전 대상을 바꿀 때 강제로 회전값을 맞추는 함수
    public void ForceSyncRotation(float pan, float tilt)
    {
        if (_panTilt != null)
        {
            _panTilt.PanAxis.Value = pan;
            _panTilt.TiltAxis.Value = tilt;
        }

        // 즉시 시각적 적용
        HandleSpectatingLogic();
    }

    #endregion
}