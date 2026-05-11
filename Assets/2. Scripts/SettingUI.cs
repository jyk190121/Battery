using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(SettingsUI))]
public class SettingsUI : MonoBehaviour
{
    public Slider sensitivitySlider;
    public TMP_Text sensitivityText;

    //public Slider[] volumSet = new Slider[3];
    [Header("Volume Settings")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    SettingsUIController controller;

    void Start()
    {
        InitMouseSettings();
        InitVolumeSettings();
    }

    void InitMouseSettings()
    {
        // 슬라이더의 최대/최소값 코드로 강제 설정
        sensitivitySlider.maxValue = 5f;
        sensitivitySlider.minValue = 0.1f;

        // 초기값 로드
        sensitivitySlider.value = GameSettingsManager.Instance.CurrentSensitivity;
        UpdateSensitivityText(sensitivitySlider.value);

        sensitivitySlider.onValueChanged.AddListener((val) => {
            GameSettingsManager.Instance.SetMouseSensitivity(val);
            UpdateSensitivityText(val);
        });
    }

    void UpdateSensitivityText(float val)
    {
        // 소수점 첫째 자리까지만 표시 (예: "감도: 2.5")
        if (sensitivityText != null) sensitivityText.text = $" {sensitivitySlider.value:F1}";
    }

    void InitVolumeSettings()
    {
        // 볼륨 슬라이더의 최소/최대값 설정 (0을 넣으면 Log 연산 오류가 나므로 0.0001 사용)
        masterSlider.minValue = 0f;
        masterSlider.maxValue = 1f;
        bgmSlider.minValue = 0f;
        bgmSlider.maxValue = 1f;
        sfxSlider.minValue = 0f;
        sfxSlider.maxValue = 1f;

        // 초기값 로드 (저장된 값이 없으면 기본값 1)
        masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 1f);
        bgmSlider.value = PlayerPrefs.GetFloat("BgmVol", 1f);
        sfxSlider.value = PlayerPrefs.GetFloat("SfxVol", 1f);

        // 값이 변경될 때마다 볼륨 조절 및 저장
        masterSlider.onValueChanged.AddListener((val) => {
            SoundManager.Instance.SetMasterVolume(val);
            PlayerPrefs.SetFloat("MasterVol", val);
        });

        bgmSlider.onValueChanged.AddListener((val) => {
            SoundManager.Instance.SetBgmVolume(val);
            PlayerPrefs.SetFloat("BgmVol", val);
        });

        sfxSlider.onValueChanged.AddListener((val) => {
            SoundManager.Instance.SetSfxVolume(val);
            PlayerPrefs.SetFloat("SfxVol", val);
        });
    }

    public void OnClickClose()
    {
        // Controller를 찾아서 토글 함수 호출
        if(TryGetComponent(out controller))
        {
            controller.ToggleSettings();
        }

        //FindObjectOfType<SettingsUIController>()?.ToggleSettings();
    }
}