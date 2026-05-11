using UnityEngine;
using TMPro;
using Unity.Netcode;

public class TabletEconomyUI : MonoBehaviour
{
    [Header("UI 텍스트 연결 (현황판)")]
    public TextMeshProUGUI currentWeekText;        // 현재 주차 (예: Week 1) - 현황판 용도
    public TextMeshProUGUI currentDayText;         // 현재 날짜 (예: Day 3) - 현황판 용도
    public TextMeshProUGUI currentGoldText;        // 현재 보유 중인 총 자산
    public TextMeshProUGUI performanceScoreText;   // 실적 점수 현황 (현재/목표)

    private void Start()
    {
        if (GameMaster.Instance == null) return;

        // 1. 초기 UI 갱신
        RefreshUI();

        // 2. 각 매니저 연결
        var economy = GameMaster.Instance.economyManager;
        var performance = GameMaster.Instance.performanceManager;
        var dayCycle = GameMaster.Instance.dayCycleManager;

        // 3. 데이터가 변할 때마다 UI가 자동으로 갱신되도록 이벤트 구독
        if (economy != null)
        {
            economy.currentTotalGold.OnValueChanged += (prev, next) => RefreshUI();
        }

        if (performance != null)
        {
            performance.dynamicTargetScore.OnValueChanged += (prev, next) => RefreshUI();
            performance.accumulatedScore.OnValueChanged += (prev, next) => RefreshUI();
        }

        if (dayCycle != null)
        {
            dayCycle.currentDayIndex.OnValueChanged += (prev, next) => RefreshUI();
        }

        // 4. 주차가 넘어갈 때(completedCycleCount 변경 시) UI를 갱신하기 위한 구독 추가
        GameMaster.Instance.completedCycleCount.OnValueChanged += (prev, next) => RefreshUI();
    }

    // 실제 UI 텍스트를 최신 데이터로 덮어쓰는 함수
    private void RefreshUI()
    {
        if (GameMaster.Instance == null) return;

        EconomyManager economy = GameMaster.Instance.economyManager;
        PerformanceManager performance = GameMaster.Instance.performanceManager;
        DayCycleManager dayCycle = GameMaster.Instance.dayCycleManager;

        // 1. 현재 주차 표시 (완료된 사이클 횟수 + 1 = 현재 주차)
        if (currentWeekText != null)
        {
            int currentWeek = GameMaster.Instance.completedCycleCount.Value + 1;
            currentWeekText.text = $"Week {currentWeek}";
        }

        // 2. 현재 날짜 표시 (게임 로직상 5일차가 정산일이므로 / 5 로 수정)
        if (currentDayText != null && dayCycle != null)
        {
            currentDayText.text = $"Day {dayCycle.currentDayIndex.Value} / 4";
        }

        // 3. 현재 보유 중인 돈 표시
        if (currentGoldText != null && economy != null)
        {
            currentGoldText.text = $"{economy.currentTotalGold.Value} G";
        }

        // 4. 주간 실적 점수 현황 표시 ( 누적 점수 / 목표 점수 )
        if (performanceScoreText != null && performance != null)
        {
            performanceScoreText.text = $"{performance.accumulatedScore.Value} / {performance.dynamicTargetScore.Value}";
        }
    }

    // UI창이 활성화 될 때마다 한 번씩 강제 갱신
    private void OnEnable()
    {
        RefreshUI();
    }
}