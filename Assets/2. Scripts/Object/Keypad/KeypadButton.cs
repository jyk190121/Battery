using UnityEngine;

public class KeypadButton : MonoBehaviour
{
    [Header("버튼 정보")]
    public int buttonValue; // 인스펙터에서 이 버튼의 숫자를 지정해주세요 (0~9)
    public KeypadController controller; // 신호를 보낼 본체 컨트롤러

    public void PressButton()
    {
        if (controller != null)
        {
            controller.AddInput(buttonValue);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}에 KeypadController가 연결되지 않았습니다!");
        }
    }
}