using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public enum BatteryMode
{
    Infinite,           // 배터리가 항상 100% (소모되지 않음)
    DrainWhenActive     // 폰이 켜져 있을 때만 소모
}

public class PhoneBatteryController : MonoBehaviour
{
    public static PhoneBatteryController Instance;

    [Header("배터리 설정")]
    public BatteryMode currentMode = BatteryMode.DrainWhenActive;
    public float maxBattery = 1000f;
    public float currentBattery;

    [Tooltip("1초당 소모되는 배터리 량")]
    public float drainRatePerSecond = 1f;

    [Header("UI 연결")]
    public Image batteryFillImage;
    public TextMeshProUGUI batteryText;

    // 배터리가 다 되었을 때 알림을 보낼 이벤트
    public event Action OnBatteryEmpty;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // 씬 시작 시 배터리 초기화 (필요에 따라 저장된 데이터에서 불러오도록 수정 가능)
        currentBattery = maxBattery;
    }

    private void Start()
    {
        UpdateBatteryUI(); // 초기 UI 갱신
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 100%로 풀 충전
        currentBattery = maxBattery;

        // 씬 이름에 따른 소모 모드 설정
        if (scene.name == "KJY_Lobby")
        {
            currentMode = BatteryMode.Infinite;
        }
        else if (scene.name == "KJY_Player")
        {
            currentMode = BatteryMode.DrainWhenActive;
        }

        // 충전되었으므로 폰을 켤 수 있도록 전력 상태 복구 (PhoneUIController의 전력 여부)
        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.hasPower = true;
        }

        // 화면 갱신
        UpdateBatteryUI();
    }

    private void Update()
    {
        // 무한 모드일 경우 배터리 소모 로직을 무시합니다.
        if (currentMode == BatteryMode.Infinite) return;

        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isPhoneActive)
        {
            if (currentBattery > 0)
            {
                currentBattery -= drainRatePerSecond * Time.deltaTime;
                UpdateBatteryUI(); // 배터리가 닳을 때마다 UI 갱신

                if (currentBattery <= 0)
                {
                    currentBattery = 0;
                    UpdateBatteryUI();
                    OnBatteryEmpty?.Invoke();
                }
            }
        }
    }

    private void UpdateBatteryUI()
    {
        if (batteryFillImage == null || batteryText == null) return;

        // 이미지 게이지 처리 (1.0 ~ 0.0)
        float ratio = currentBattery / maxBattery;
        batteryFillImage.fillAmount = ratio;

        // 텍스트 퍼센트 처리 (올림을 사용하여 0.1%라도 남아있으면 1%로 표시)
        int percent = Mathf.CeilToInt(ratio * 100f);
        batteryText.text = $"{percent}%";
    }
}