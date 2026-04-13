using UnityEngine;
using Unity.Netcode;

public class EconomyManager : NetworkBehaviour
{
    [Header("Economy Settings")]
    public int baseWeeklyQuota = 1000;
    public int quotaGrowthAmount = 500;       // 매 사이클 성공 시 증가할 할당량
    public float loanLimitRate = 0.5f;
    public float penaltyInterestRate = 1.1f;

    [Header("Synced Economy Data")]
    public NetworkVariable<int> dynamicWeeklyQuota = new NetworkVariable<int>(1000, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> accumulatedRepayment = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> availableLoanLimit = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 내부 추적용: 현재 주차의 순수 목표액
    private int currentCycleBaseQuota;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentCycleBaseQuota = baseWeeklyQuota;
            dynamicWeeklyQuota.Value = baseWeeklyQuota;
        }
    }

    // [서버 전용] GameMaster가 직접 호출 (이벤트 기반 X, 순서 확정)
    public void ProcessDailyIncome(int totalDailyIncome, int currentDayIndex)
    {
        if (!IsServer) return;

        Debug.Log($"정산 시작: Day {currentDayIndex} / 오늘 수익 {totalDailyIncome}");

        int newLoanLimit = Mathf.FloorToInt(totalDailyIncome * loanLimitRate);
        availableLoanLimit.Value += newLoanLimit;

        int requiredMinimum = GetDailyMinimumRequired(currentDayIndex);

        accumulatedRepayment.Value += totalDailyIncome;

        if (totalDailyIncome < requiredMinimum)
        {
            int shortfall = requiredMinimum - totalDailyIncome;
            int penaltyAddedToQuota = Mathf.CeilToInt(shortfall * penaltyInterestRate);
            dynamicWeeklyQuota.Value += penaltyAddedToQuota;
            Debug.Log($"<color=red>실적 미달! {shortfall} 부족하여 {penaltyAddedToQuota} 패널티 누적</color>");
        }
        else
        {
            Debug.Log($"<color=green>최소 실적({requiredMinimum}) 달성 성공!</color>");
        }
    }

    // [서버 전용] 사이클 클리어 시 GameMaster가 호출하여 다음 주차 세팅
    public void PrepareNextWeek()
    {
        if (!IsServer) return;

        currentCycleBaseQuota += quotaGrowthAmount;
        dynamicWeeklyQuota.Value = currentCycleBaseQuota;
        accumulatedRepayment.Value = 0;
        // availableLoanLimit.Value 는 0으로 리셋하지 않고 이월시킵니다.

        Debug.Log($"<color=magenta>새로운 사이클 목표 빚: {dynamicWeeklyQuota.Value} Gold</color>");
    }

    public bool TryPurchaseWithLoan(int price)
    {
        if (!IsServer) return false;

        if (availableLoanLimit.Value >= price)
        {
            availableLoanLimit.Value -= price;
            dynamicWeeklyQuota.Value += price;
            return true;
        }
        return false;
    }

    public int GetDailyMinimumRequired(int dayIndex)
    {
        float[] dayRates = { 0f, 0.1f, 0.15f, 0.20f, 0.25f, 0.30f };
        if (dayIndex < 1 || dayIndex > 5) return 0;
        return Mathf.FloorToInt(dynamicWeeklyQuota.Value * dayRates[dayIndex]);
    }

    public bool CheckWeeklyClear()
    {
        return accumulatedRepayment.Value >= dynamicWeeklyQuota.Value;
    }

    public void ResetEconomyData()
    {
        if (!IsServer) return;
        currentCycleBaseQuota = baseWeeklyQuota;
        dynamicWeeklyQuota.Value = baseWeeklyQuota;
        accumulatedRepayment.Value = 0;
        availableLoanLimit.Value = 0;
    }
}