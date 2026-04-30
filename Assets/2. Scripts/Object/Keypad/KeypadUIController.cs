using UnityEngine;
using TMPro; // UI 텍스트를 사용한다면 활성화

public class KeypadUIController : MonoBehaviour
{
    public string correctPassword = "1234";
    private string currentInput = "";

    [Header("연결")]
    public TMP_Text displayText; // 입력된 숫자를 보여줄 텍스트 (옵션)
    public SafeDoor safeDoor;    // 열어야 할 문 스크립트

    private void OnEnable()
    {
        // UI가 켜질 때마다 입력 초기화
        ClearInput();
    }

    // UI의 0~9 버튼 클릭 이벤트(OnClick)에 연결할 함수
    public void AddNumber(int number)
    {
        // 4자리까지만 입력 가능
        if (currentInput.Length < 4)
        {
            currentInput += number.ToString();
            UpdateDisplay();
        }
    }

    // UI의 엔터 버튼 클릭 이벤트(OnClick)에 연결할 함수
    public void SubmitPassword()
    {
        // 1. 유효성 검사
        if (currentInput == correctPassword)
        {
            Debug.Log("비밀번호 일치! 문을 엽니다.");
            safeDoor.OpenDoor();
            CloseUI(); // 맞으면 UI 닫기
        }
        else
        {
            Debug.Log("비밀번호 오류!");
        }

        // 2. 검사 후 써진 비밀번호 지우기
        ClearInput();
    }

    private void ClearInput()
    {
        currentInput = "";
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (displayText != null)
        {
            displayText.text = currentInput;
        }
    }

    public void CloseUI()
    {
        // 마우스 커서 숨기기 (다시 FPS 상태로 복귀)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        gameObject.SetActive(false);
    }
}