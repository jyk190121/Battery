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

    [Header("폰사용중 상태 확인")]
    public bool isHoldingSmartphone = false; // [추가] 스마트폰 사용 여부

    private CinemachinePanTilt _panTilt;

    // 1. 상하 회전값 동기화를 위한 변수 (서버 권한)
    public NetworkVariable<float> NetVerticalRotation = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


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

    }

    void Update()
    {
        //if (IsOwner)
        //{
        //    // 몬스터에게 잡힌 상태(isSnared)라면 회전 로직 건너뛰기
        //    if (playerController != null && playerController.isSnared.Value) return;

        //    HandleRotation();

        //    // 내 회전값을 다른 사람(관전자)들이 알 수 있게 갱신
        //    if (_panTilt != null)
        //        NetVerticalRotation.Value = _panTilt.TiltAxis.Value;
        //}
        //else if (_isSpectating && _spectatingTarget != null)
        //{
        //    // 2. 관전 중이라면 대상의 회전값을 내 카메라에 강제 적용
        //    ApplyTargetRotation();
        //}
        if (IsOwner)
        {
            if (playerController != null && playerController.isSnared.Value) return;

            HandleRotation();

            // 내 회전값을 네트워크 변수에 동기화 (관전자들이 볼 수 있게)
            if (_panTilt != null) NetVerticalRotation.Value = _panTilt.TiltAxis.Value;
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

        // 2. 관전 모드 우선 처리
        if (_isSpectating)
        {
            //// 관전 대상이 유효한지 체크
            //if (_spectatingTarget != null && _spectatingTarget.gameObject != null)
            //{
            //    HandleSpectatingLogic();
            //}
            //else
            //{
            //    // 대상이 없으면 내 카메라로 복귀 시도
            //    SetSpectatingTarget(null);
            //}
            //return;
            HandleSpectatingLogic();
            return;
        }

        // 3. 태블릿이나 사망 상태 체크
        if (_isTabletOpen) return;

        // 4. 입력 처리
        Vector2 mouseDelta = Input.GetMouseDelta();
        _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;
        float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
        _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

        // 5. 상태별 회전 적용
        if (playerController != null && playerController.isDead.Value)
        {
            // 사망 시 처리
            ApplyDeathRotation();

            // 사망 시에는 캐릭터가 누워있으므로 카메라 위치를 조금 높여주는 것이 좋습니다.
            Vector3 deathCameraPos = transform.position + Vector3.up * 1.5f; // 캐릭터 발 위치가 아닌 공중에서 내려다보게 함
            vcam.transform.position = Vector3.Lerp(vcam.transform.position, deathCameraPos, Time.deltaTime * transitionSpeed);
        }
        else
        {
            ApplyLivingRotation();
        }

        //// 1. 마우스 입력 데이터 가져오기
        //Vector2 mouseDelta = Input.GetMouseDelta();

        //// 2. 좌우 회전 (Pan) 업데이트
        //_panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

        //// 3. 상하 회전 (Tilt) 업데이트 (이 부분이 빠져있어서 막힌 느낌이 듭니다)
        //// 마우스 Y는 위로 올릴 때 (+), 아래로 내릴 때 (-)이므로 시선 처리를 위해 빼줍니다.
        //float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);

        //// 상하 회전 각도 제한 (예: -70도 ~ 70도)
        //_panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

        //if (playerController.isDead.Value)
        //{
        //    ApplyDeathRotation();
        //}
        //else
        //{
        //    // 앉아있을 때 시선을 고정하고 싶다면 여기서 조건을 분기할 수 있습니다.
        //    if (playerMove != null && playerMove.IsCrouching)
        //    {
        //         _panTilt.TiltAxis.Value = -10f;
        //    }

        //    ApplyLivingRotation();
        //}
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
    #region 사망 시 로테이션 변경 점 처리 함수
    void HandleSpectatingLogic()
    {
        //if (_isSpectating && _spectatingTarget != null)
        //{
        //    // 1. 위치 동기화 (눈높이)
        //    if (_spectatingTarget.cameraTarget != null)
        //    {
        //        vcam.transform.position = _spectatingTarget.cameraTarget.position;
        //    }

        //    // 2. 회전 동기화 (직접 계산)
        //    // 좌우(Pan): 대상 플레이어 몸체의 Y축 회전값 사용
        //    float targetPan = _spectatingTarget.transform.eulerAngles.y;
        //    // 상하(Tilt): 서버에서 동기화된 NetworkVariable 값 사용
        //    float targetTilt = _spectatingTarget.NetVerticalRotation.Value;

        //    // 월드 좌표 기준으로 쿼터니언 생성 (Z축을 0으로 고정하여 일어선 시점 유지)
        //    Quaternion combinedRotation = Quaternion.Euler(targetTilt, targetPan, 0);

        //    // 3. 카메라에 즉시 적용
        //    vcam.transform.rotation = combinedRotation;
        //    vcam.Lens.Dutch = 0; // 화면 기울어짐 방지

        //    if (CameraGroup != null)
        //    {
        //        CameraGroup.transform.rotation = combinedRotation;
        //    }
        //}
        //else if (_isSpectating)
        //{
        //    SetSpectatingTarget(null);
        //}

        if (_isSpectating && _spectatingTarget != null)
        {
            // 1. 위치 동기화: 관전 대상의 눈(cameraTarget) 위치를 그대로 따라감
            if (_spectatingTarget.cameraTarget != null)
            {
                // 타겟이 누워있다면 cameraTarget도 바닥 근처에 있을 것입니다. 그 위치를 그대로 가져옵니다.
                vcam.transform.position = _spectatingTarget.cameraTarget.position;
            }

            // 2. 회전 동기화: 대상의 몸이나 부모의 회전은 무시하고 "입력된 각도"만 조립
            // 대상의 GetCurrentPan/Tilt는 _panTilt 수치이므로 몸이 누워있어도 영향을 받지 않습니다.
            float targetPan = _spectatingTarget.GetCurrentPan();
            float targetTilt = _spectatingTarget.NetVerticalRotation.Value; // 서버 동기화된 Tilt 사용

            // Z축을 0으로 강제하여 화면이 옆으로 기우는 현상을 원천 차단 (서 있는 상태의 로테이션)
            Quaternion uprightRotation = Quaternion.Euler(targetTilt, targetPan, 0);

            // 3. 카메라와 시각적 그룹에 즉시 적용
            vcam.transform.rotation = uprightRotation;
            vcam.Lens.Dutch = 0; // 시네머신 자체 기울기 초기화

            if (CameraGroup != null)
            {
                CameraGroup.transform.rotation = uprightRotation;
            }
        }
        else if (_isSpectating)
        {
            SetSpectatingTarget(null);
        }
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
        _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

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

            float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
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
        //if (_panTilt != null) _panTilt.enabled = !_isSpectating;

        if (vcam == null) TryFindCamera();
        if (vcam == null) return;

        if (_isSpectating)
        {
            //if (_panTilt != null) _panTilt.enabled = false;

            //// 2. 카메라 대상을 관전 타겟으로 변경
            //vcam.Follow = target.cameraTarget;

            //// 3. 전환 시 튀는 현상 방지
            //vcam.OnTargetObjectWarped(target.cameraTarget, target.cameraTarget.position - vcam.transform.position);

            if (_panTilt != null) _panTilt.enabled = false;
            vcam.Follow = target.cameraTarget;

            // 관전 시작 시 카메라가 기울어지지 않도록 초기화
            vcam.Lens.Dutch = 0;
            vcam.transform.localRotation = Quaternion.identity;

            vcam.OnTargetObjectWarped(target.cameraTarget, target.cameraTarget.position - vcam.transform.position);
        }
        else
        {
            vcam.Follow = cameraTarget;
            if (_panTilt != null)
            {
                _panTilt.enabled = true;
                _panTilt.PanAxis.Value = transform.eulerAngles.y;
                _panTilt.TiltAxis.Value = 0; // 복귀 시 정면 응시
            }
        }
    }

    public void SetSpectatingMode(bool isSpectating)
    {

        if (vcam != null && _panTilt != null) _panTilt.enabled = !isSpectating;
        //if (isSpectating) vcam.transform.localRotation = Quaternion.identity;
        if (isSpectating)
        {
            // 2. vcam이 null인지도 확인해야 안전합니다.
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
        // 1. 시네머신 가상 카메라 자체의 기울기(Dutch)를 0으로 강제 고정
        if (vcam != null)
        {
            vcam.Lens.Dutch = 0;
            // 부모가 누워있으므로 로컬 회전이 아닌 월드 회전(rotation)을 직접 제어해야 합니다.
            vcam.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);
            vcam.transform.position = cameraTarget.position + Vector3.up * 0.1f;
        }

        // 2. 휴대폰 모델 등 시각적 그룹도 월드 기준으로 세워줌
        if (CameraGroup != null)
        {
            // 부모(캐릭터 몸)가 누워있어도 시선은 Pan/Tilt 값만 따르도록 월드 회전 적용
            CameraGroup.transform.rotation = Quaternion.Euler(_panTilt.TiltAxis.Value, _panTilt.PanAxis.Value, 0);
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
    public float GetCurrentPan() => _panTilt != null ? _panTilt.PanAxis.Value : transform.eulerAngles.y;
    public float GetCurrentTilt() => _panTilt != null ? _panTilt.TiltAxis.Value : 0f;
    #endregion
}