using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;

public class TabletEconomyUI : MonoBehaviour
{
    [Header("UI 텍스트 연결")]
    public TextMeshProUGUI totalDebtText;         // 주간 총 할당량 (Total Debt)
    public TextMeshProUGUI dailyRequiredText;     // 오늘 달성해야 할 최소액 (Today's Debt)
    public TextMeshProUGUI availableLoanText;     // 상점 사용 가능 자금
    public TextMeshProUGUI rateText;              // 달성률 (%)
    public Image DeftPercent;
    

    private void Start()
    {
        if (GameMaster.Instance == null || GameMaster.Instance.economyManager == null) return;

        // 1. 초기 UI 갱신
        RefreshUI();

        // 2. 데이터가 변할 때마다 UI가 자동으로 갱신되도록 이벤트 구독 (구독 형태: OnValueChanged += (이전값, 새로운값) => 실행할함수)
        var economy = GameMaster.Instance.economyManager;
        var dayCycle = GameMaster.Instance.dayCycleManager;

        economy.dynamicWeeklyQuota.OnValueChanged += (prev, next) => RefreshUI();
        economy.accumulatedRepayment.OnValueChanged += (prev, next) => RefreshUI();
        economy.availableLoanLimit.OnValueChanged += (prev, next) => RefreshUI();
        dayCycle.currentDayIndex.OnValueChanged += (prev, next) => RefreshUI();
    }

    private void OnDestroy()
    {
        // 씬 전환이나 오브젝트 파괴 시 이벤트 구독 해제 (메모리 누수 방지)
        if (GameMaster.Instance != null && GameMaster.Instance.economyManager != null)
        {
            var economy = GameMaster.Instance.economyManager;
            var dayCycle = GameMaster.Instance.dayCycleManager;

            // 람다식을 사용했기 때문에 엄밀한 해제는 어렵지만, NetworkVariable은 오브젝트 파괴 시 자동 정리되는 편입니다.
            // 안전을 위해 OnNetworkDespawn 등을 활용하는 것이 Netcode 표준에 더 맞습니다.
        }
    }

    // 실제 UI 텍스트를 최신 데이터로 덮어쓰는 함수
    private void RefreshUI()
    {
        if (GameMaster.Instance == null) return;

        EconomyManager economy = GameMaster.Instance.economyManager;
        int currentDay = GameMaster.Instance.dayCycleManager.currentDayIndex.Value;

        // 1. 주간 할당량 (목표 빚)
        if (totalDebtText != null)
            totalDebtText.text = $"{economy.dynamicWeeklyQuota.Value}";

        // 2. 오늘의 최소 요구액
        if (dailyRequiredText != null)
            dailyRequiredText.text = $"{economy.GetDailyMinimumRequired(currentDay)}";

 //$"{economy.accumulatedRepayment.Value}";

        // 3. 쇼핑 가능 자금
        if (availableLoanText != null)
            availableLoanText.text = $"{economy.availableLoanLimit.Value}";

        // 4. 달성률 (Rate %) 계산
        if (rateText != null)
        {
            float target = economy.dynamicWeeklyQuota.Value;
            float current = economy.accumulatedRepayment.Value;

            float rate = (target > 0) ? (current / target) * 100f : 0f;
            rateText.text = $"{rate:F1} %"; // 소수점 1자리까지만 표시 (예: 35.5 %)
        }

        if(DeftPercent != null)
        {
            float percent = economy.accumulatedRepayment.Value / economy.dynamicWeeklyQuota.Value;
            DeftPercent.fillAmount = percent;
        }
    }

    // UI창이 활성화 될 때마다 한 번씩 강제 갱신 (선택 사항)
    private void OnEnable()
    {
        RefreshUI();
    }
}