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
    public float crouchYPos = -0.5f;        // 추가: 앉았을 때 카메라 높이 (필요에 따라 조절)

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
                    vcam.LookAt = cameraTarget;

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

        //if (_panTilt != null)
        //{
        //    Vector2 mouseDelta = Input.GetMouseDelta();

        //    // 1. 좌우 회전 (Pan)
        //    _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

        //    // 2. 상하 회전 (Tilt) 및 제한
        //    float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
        //    _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

        //    // 3. 본체 회전 동기화 (Y축만)
        //    transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);

        //    UpdateCameraPosition();
        //}

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
            // 앉았을 때 정면을 보게 강제하고 싶다면 아래 주석 해제 (부드럽게 정렬됨)
            // _panTilt.TiltAxis.Value = Mathf.Lerp(_panTilt.TiltAxis.Value, 0, Time.deltaTime * transitionSpeed);
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

        // 타겟 Y값 결정 (우선순위: 앉기 > 달리기 > 걷기)
        float targetY = walkYPos;
        if (isCrouching) targetY = crouchYPos;
        else if (isRunning) targetY = runYPos;


        // 현재 로컬 위치 가져오기
        Vector3 currentPos = cameraTarget.localPosition;

        // Y값만 부드럽게 보간(Lerp)
        float newY = Mathf.Lerp(currentPos.y, targetY + originYoffset, Time.deltaTime * transitionSpeed);

        // 새로운 위치 적용
        cameraTarget.localPosition = new Vector3(currentPos.x, newY, currentPos.z);
    }
}