using Unity.Cinemachine;
using UnityEngine;

public class PlayerRotation : MonoBehaviour
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

    void Start()
    {
        if (vcam != null)
        {
            _panTilt = vcam.GetComponent<CinemachinePanTilt>();
        }
        
        if(playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        // 초기 위치 설정
        if (cameraTarget != null)
        {
            Vector3 pos = cameraTarget.localPosition;
            cameraTarget.localPosition = new Vector3(pos.x, walkYPos + originYoffset, pos.z);
        }

        // 마우스 커서를 화면 중앙에 고정하고 숨깁니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        // 1. Input.cs에서 마우스 이동량 가져오기
        Vector2 mouseDelta = Input.GetMouseDelta();

        //if (_panTilt != null)
        //{
        //    // 2. 시네머신 PanTilt 값 업데이트
        //    // Pan (좌우 회전), Tilt (상하 회전)
        //    _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;
        //    _panTilt.TiltAxis.Value -= mouseDelta.y * sensitivity; // 위로 올리면 화면이 위를 보게 함

        //    // 3. 플레이어 본체(몸) 회전 동기화
        //    // 카메라가 보는 수평 방향(Pan)으로 몸을 돌려줘야 이동(WASD) 방향이 일치하게 됩니다.
        //    transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);
        //}

        if (_panTilt != null)
        {

            // 1. 좌우 회전은 자유롭게 (0~360도)
            _panTilt.PanAxis.Value += mouseDelta.x * sensitivity;

            // 2. 상하 회전값 계산
            float newTilt = _panTilt.TiltAxis.Value - (mouseDelta.y * sensitivity);

            // 3. Mathf.Clamp를 사용하여 범위를 제한 (-70도 ~ 70도)
            // 위로 70도(-70), 아래로 30도(30)
            _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 70f);

            // 4. 본체 회전 동기화
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