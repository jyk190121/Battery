using Unity.Cinemachine;
using UnityEngine;

public class PlayerRotation : MonoBehaviour
{
    [Header("참조")]
    public CinemachineCamera vcam; // 인스펙터에서 시네머신 카메라 할당

    [Header("설정")]
    public float sensitivity = 0.05f;

    private CinemachinePanTilt _panTilt;

    void Start()
    {
        if (vcam != null)
            _panTilt = vcam.GetComponent<CinemachinePanTilt>();

        // 마우스 커서를 화면 중앙에 고정하고 숨깁니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
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

            // 3. Mathf.Clamp를 사용하여 범위를 제한 (-70도 ~ 30도)
            // 위로 70도(-70), 아래로 30도(30)
            _panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, -70f, 30f);

            // 3. 본체 회전 동기화
            transform.rotation = Quaternion.Euler(0, _panTilt.PanAxis.Value, 0);
        }
    }
}