using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NumberpadController : MonoBehaviour
{
    [Header("Buttons")]
    public Button[] buttons; // 1~9, 0 순서
    public Button EnterButton;
    public Button ClearButton;

    [Header("UI References")]
    public TextMeshProUGUI numberVisual;

    [Header("Settings")]
    public string inputCode = "";
    public int maxCodeLength = 4;

    public NumberPadInteraction numberPadInteraction; // 상호작용 스크립트 참조

    public Door door; // 연결된 문 오브젝트 참조

    void Start()
    {
        // 스크립트 시작 시 버튼 이벤트 리스너를 등록합니다.
        SetBtnValue();
        UpdateVisual(); // 초기 화면 텍스트 설정
    }

    void SetBtnValue()
    {
        // 1. 숫자 버튼 리스너 등록
        for (int i = 0; i < buttons.Length; i++)
        {
            // 인덱스 0~8은 숫자 1~9, 인덱스 9는 숫자 0으로 매칭
            int numberValue = (i == 9) ? 0 : i + 1;
            string numStr = numberValue.ToString();

            // Action 람다식 캡처 문제(클로저 이슈)를 피하기 위해 numStr 지역변수 사용
            buttons[i].onClick.AddListener(() => OnNumberClicked(numStr));
        }

        // 2. 클리어 및 엔터 버튼 리스너 등록
        ClearButton.onClick.AddListener(ClearInput);
        EnterButton.onClick.AddListener(OnEnterClicked);
    }

    // 숫자 버튼이 눌렸을 때 호출되는 메서드
    public void OnNumberClicked(string num)
    {
        // 현재 입력된 자리수가 최대 자리수(4)보다 작을 때만 숫자 추가
        if (inputCode.Length < maxCodeLength)
        {
            inputCode += num;
            UpdateVisual();
        }
    }

    // 초기화 처리 메서드
    public void ClearInput()
    {
        inputCode = "";
        UpdateVisual();
    }

    // 엔터 버튼 처리 메서드
    public void OnEnterClicked()
    {
        numberPadInteraction.CloseKeypadUI(); // UI 닫기
        door.checkPassword(inputCode);
        ClearInput();
    }

    // UI 텍스트 업데이트 메서드
    private void UpdateVisual()
    {
        if (numberVisual != null)
        {
            numberVisual.text = inputCode;
        }
    }
}