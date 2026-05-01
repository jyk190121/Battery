using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class NumberPadInteraction : MonoBehaviour
{
    private Camera cam;
    public float interactDistance = 3f;
    public LayerMask NumberpadLayer;
    public TextMeshProUGUI promptText; // 상호작용 프롬프트 텍스트 (예: "Press E to interact")

    public static event Action<bool> OnKeypadUIOpened; // 키패드 UI가 열릴 때 발생하는 이벤트

    [Header("UI 설정")]
    public GameObject keypadUI; // 띄울 키패드 UI 패널 (Canvas)

    private void Awake()
    {
        cam = Camera.main;
    }
    private void Update()
    {
        if (keypadUI == null)
        {
            return;
        }

        // UI가 열려있을 때 닫는 기능 추가
        if (keypadUI.activeSelf)
        {
            // 예: ESC 키를 누르면 닫히도록 설정
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseKeypadUI();
            }
            return; // UI가 켜져 있으면 아래의 Raycast(상호작용) 코드는 실행하지 않음
        }

        // 화면 정중앙(크로스헤어)에서 Ray 쏘기
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            if ((NumberpadLayer.value & (1 << hit.collider.gameObject.layer)) > 0)
            {
                promptText.text = "Press E to interact";

                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    OpenKeypadUI();
                }
            }
            else
            {
                promptText.text = "";
            }
        }
        else
        {
            promptText.text = "";
        }
    }

    private void OpenKeypadUI()
    {
        keypadUI.SetActive(true);
        promptText.text = "";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // UI가 열렸다는 사실을 이벤트로 알림 (true 전달)
        OnKeypadUIOpened?.Invoke(true);
    }

    // UI 닫기 메서드 추가
    public void CloseKeypadUI()
    {
        keypadUI.SetActive(false);

        // 다시 마우스를 잠그고 숨김
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // UI가 닫혔다는 사실을 이벤트로 알림 (false 전달)
        OnKeypadUIOpened?.Invoke(false);
    }
}
