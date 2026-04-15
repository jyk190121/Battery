using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerRotation : NetworkBehaviour
{
    [Header("참조")]
    public CinemachineCamera vcam;  // 인스펙터에서 시네머신 카메라 할당
    public Transform cameraTarget;  // eye_Cinemachine 오브젝트를 여기에 할당
    public PlayerMove playerMove;   // 이동 속도를 체크하기 위해 참조

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
    public float crouchYPos = 1.0f;         // 앉았을 때 카메라 위치 앞으로 조정

    private CinemachinePanTilt _panTilt;

    public override void OnNetworkSpawn()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        TryFindCamera();
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

        Vector2 mouseDelta = Input.GetMouseDelta();

        // 1. 좌우 회전 (Pan) - 언제나 가능
        _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

        // 2. 상하 회전 (Tilt) - 앉아있을 때는 입력을 무시함
        // PlayerMove의 isCrouching 변수가 private이라면 public bool IsCrouching => isCrouching; 등으로 공개되어 있어야 합니다.
        if (playerMove != null && !playerMove.IsCrouching)
        {
            float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
            _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);
        }
        else if (playerMove != null && playerMove.IsCrouching)
        {
            float crouchViewOffset = -20f;

            //// 앉았을 때 정면을 보게 강제하고 싶다면 아래 주석 해제 (부드럽게 정렬됨)
            //_panTilt.TiltAxis.Value = Mathf.Lerp(_panTilt.TiltAxis.Value, crouchViewOffset, Time.deltaTime * transitionSpeed);

            //if (Mathf.Abs(_panTilt.TiltAxis.Value) < 0.1f) _panTilt.TiltAxis.Value = crouchViewOffset;

            // Lerp 대신 직접 값을 대입해서 변화가 있는지 먼저 확인하세요.
            _panTilt.TiltAxis.Value = crouchViewOffset;
        }

        // 3. 본체 회전 동기화
        transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);

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
}