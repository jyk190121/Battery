using UnityEngine;
using TMPro;

public class PhoneMainTopbarUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI WeeklyPaymentText;
    public TextMeshProUGUI DayWeekdayText;

    private void Start()
    {
        // 1. 매니저들이 준비되지 않았다면 에러 방지
        if (GameMaster.Instance == null || GameMaster.Instance.economyManager == null || GameMaster.Instance.dayCycleManager == null)
            return;

        // 2. 처음 켜질 때 현재 값으로 한 번 텍스트를 채워줌
        RefreshUI();

        // 3. 값(할당량이나 날짜)이 '변할 때만' RefreshUI가 실행되도록 구독
        GameMaster.Instance.economyManager.dynamicWeeklyQuota.OnValueChanged += OnDataChanged;
        GameMaster.Instance.dayCycleManager.currentDayIndex.OnValueChanged += OnDataChanged;
    }

    private void OnDestroy()
    {
        // 4. 오브젝트가 파괴될 때 메모리 누수를 막기 위해 구독 해제
        if (GameMaster.Instance != null)
        {
            if (GameMaster.Instance.economyManager != null)
                GameMaster.Instance.economyManager.dynamicWeeklyQuota.OnValueChanged -= OnDataChanged;

            if (GameMaster.Instance.dayCycleManager != null)
                GameMaster.Instance.dayCycleManager.currentDayIndex.OnValueChanged -= OnDataChanged;
        }
    }

    // NetworkVariable의 OnValueChanged 이벤트 규칙에 맞춘 징검다리 함수
    private void OnDataChanged(int previousValue, int newValue)
    {
        RefreshUI();
    }

    // Update() 대신, 값이 변할 때만 딱 1번 실행되는 함수
    private void RefreshUI()
    {
        if (GameMaster.Instance == null) return;

        var economy = GameMaster.Instance.economyManager;
        var dayCycle = GameMaster.Instance.dayCycleManager;

        WeeklyPaymentText.text = $"Weekly: {economy.dynamicWeeklyQuota.Value}";

        int currentDay = dayCycle.currentDayIndex.Value;
        DayWeekdayText.text = $"Day: {currentDay} \nDaily: {economy.GetDailyMinimumRequired(currentDay)}";
    }
}
