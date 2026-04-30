using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSafeInteractor : MonoBehaviour
{
    private Camera cam;
    public float interactDistance = 3f;
    public LayerMask numberpad;

    [Header("UI 설정")]
    public GameObject keypadUI; // 띄울 키패드 UI 패널 (Canvas)

    private void Awake()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        // 키패드 UI가 켜져 있으면 상호작용(Raycast) 중지
        if (keypadUI.activeSelf) return;

        // 화면 정중앙(크로스헤어)에서 Ray 쏘기
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // 맞은 오브젝트의 태그가 "Safe"일 때
            if (hit.collider.gameObject.layer == numberpad)
            {
                // E키를 누르면 UI 띄우기 (New Input System)
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    OpenKeypadUI();
                }
            }
        }
    }

    private void OpenKeypadUI()
    {
        keypadUI.SetActive(true);

        // UI 버튼 클릭을 위해 마우스 커서 표시 및 잠금 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}