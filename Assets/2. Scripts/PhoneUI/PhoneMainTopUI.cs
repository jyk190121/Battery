using TMPro;
using UnityEngine;

public class PhoneMainTopUI : MonoBehaviour
{
    public TextMeshProUGUI dayWeekText;
    public TextMeshProUGUI performanceScoreText;
    public TextMeshProUGUI moneyText;

    private void Start()
    {
        UpdateUI();    
    }

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (GameMaster.Instance == null) return;

        // .Value를 붙여서 네트워크 변수 상자 안의 실제 숫자(int)를 꺼냅니다.
        int currentWeek = GameMaster.Instance.completedCycleCount.Value + 1;
        int currentDay = GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
        int currentScore = GameMaster.Instance.performanceManager.accumulatedScore.Value;
        int currentMoney = GameMaster.Instance.economyManager.currentTotalGold.Value;

        // 꺼낸 숫자를 UI 텍스트에 적용합니다.
        dayWeekText.text = $"Week {currentWeek} | Day {currentDay}";
        performanceScoreText.text = $"Score: {currentScore}P";
        moneyText.text = $"Money: {currentMoney}";
    }
}
