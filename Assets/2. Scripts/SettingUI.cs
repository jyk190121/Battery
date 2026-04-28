using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public Slider sensitivitySlider;
    public TMP_Text sensitivityText;

    void Start()
    {
        // 슬라이더의 최대값을 코드로 강제 설정
        sensitivitySlider.maxValue = 5f;
        sensitivitySlider.minValue = 0.1f;

        // 초기값 로드
        sensitivitySlider.value = GameSettingsManager.Instance.CurrentSensitivity;

        sensitivitySlider.onValueChanged.AddListener((val) => {
            GameSettingsManager.Instance.SetMouseSensitivity(val);

            // 소수점 첫째 자리까지만 표시 (예: "감도: 2.5")
            if (sensitivityText != null)
                sensitivityText.text = $" {val:F1}";
        });

        // 첫 실행 시 텍스트 업데이트
        if (sensitivityText != null)
            sensitivityText.text = $" {sensitivitySlider.value:F1}";
    }
}