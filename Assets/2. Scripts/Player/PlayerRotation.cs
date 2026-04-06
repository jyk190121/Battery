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

    private CinemachinePanTilt _panTilt;

    //void Start()
    //{
    //    if (vcam != null)
    //    {
    //        _panTilt = vcam.GetComponent<CinemachinePanTilt>();
    //    }

    //    if(playerMove == null)
    //    {
    //        playerMove = GetComponent<PlayerMove>();
    //    }

    //    // 초기 위치 설정
    //    if (cameraTarget != null)
    //    {
    //        Vector3 pos = cameraTarget.localPosition;
    //        cameraTarget.localPosition = new Vector3(pos.x, walkYPos + originYoffset, pos.z);
    //    }

    //    // 마우스 커서를 화면 중앙에 고정하고 숨깁니다.
    //    Cursor.lockState = CursorLockMode.Locked;
    //    Cursor.visible = false;
    //}

    public override void OnNetworkSpawn()
    {
        if(vcam == null)
        {
            vcam = FindAnyObjectByType<CinemachineCamera>();
        }

        if (vcam != null)
        {
            _panTilt = vcam.GetComponent<CinemachinePanTilt>();
        }

        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }
        //vcam = FindAnyObjectByType<CinemachineCamera>();

        //if (vcam != null)
        //{
        //    _panTilt = vcam.GetComponent<CinemachinePanTilt>();
        //}


        //if (IsOwner)
        //{
        //    // [에러 해결] .Enabled 대신 .enabled 사용 (소문자)
        //    if (vcam != null)
        //    {
        //        vcam.Priority = 10;
        //        vcam.enabled = true;

        //        // 혹은 게임 오브젝트 전체를 끄고 켜는 방식이 가장 확실합니다.
        //        // vcam.gameObject.SetActive(true);
        //    }

        //    // 초기 위치 설정
        //    if (cameraTarget != null)
        //    {
        //        Vector3 pos = cameraTarget.localPosition;
        //        cameraTarget.localPosition = new Vector3(pos.x, walkYPos + originYoffset, pos.z);
        //    }

        //    Cursor.lockState = CursorLockMode.Locked;
        //    Cursor.visible = false;
        //}
        //else
        //{
        //    // 내 캐릭터가 아니라면 카메라 컴포넌트 비활성화
        //    if (vcam != null)
        //    {
        //        vcam.enabled = false;
        //        // vcam.gameObject.SetActive(false); // 추천: 카메라 오브젝트 자체를 끄기
        //    }
        //}

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
        
        //Vector2 mouseDelta = Input.GetMouseDelta();

        //if (_panTilt != null)
        //{

        //    // 1. 좌우 회전은 자유롭게 (0~360도)
        //    _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

        //    // 2. 상하 회전값 계산
        //    float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);

        //    // 3. Mathf.Clamp를 사용하여 범위를 제한 (-70도 ~ 70도)
        //    // 위로 70도(-70), 아래로 30도(30)
        //    _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

        //    // 4. 본체 회전 동기화
        //    transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);

        //    UpdateCameraPosition();
        //}

        if (_panTilt != null)
        {
            Vector2 mouseDelta = Input.GetMouseDelta();

            // 1. 좌우 회전 (Pan)
            _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

            // 2. 상하 회전 (Tilt) 및 제한
            float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);
            _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

            // 3. 본체 회전 동기화 (Y축만)
            transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);

            UpdateCameraPosition();
        }
    }

    void UpdateCameraPosition()
    {
        if (cameraTarget == null || playerMove == null) return;

        // 현재 속도가 걷기 속도(3.5)보다 크면 달리는 것으로 판단
        // (PlayerMove 스크립트의 currentSpeed가 public이어야 합니다)
        bool isRunning = playerMove.currentSpeed > playerMove.walkSpeed + 0.1f;

        float targetY = isRunning ? runYPos : walkYPos;

        // 현재 로컬 위치 가져오기
        Vector3 currentPos = cameraTarget.localPosition;

        // Y값만 부드럽게 보간(Lerp)
        float newY = Mathf.Lerp(currentPos.y, targetY + originYoffset, Time.deltaTime * transitionSpeed);

        // 새로운 위치 적용
        cameraTarget.localPosition = new Vector3(currentPos.x, newY, currentPos.z);
    }
}