using UnityEngine;
using UnityEngine.UI; // Image(게이지바) 사용을 위해 필요
using TMPro;
using Unity.Netcode;

public class TabletUIManager : MonoBehaviour
{
    [Header("UI 텍스트 연결")]
    public TextMeshProUGUI loanLimitText;     // 1. 대출 가능 금액
    public TextMeshProUGUI weeklyQuotaText;   // 2. 주간 할당량
    public TextMeshProUGUI dailyMinimumText;  // 3. 일일 할당량

    [Header("UI 프로그레스 바 연결")]
    public Image quotaProgressBar;            // 4. 달성 정도 (0.0 ~ 1.0)
    public TextMeshProUGUI progressPercentText; // (선택) "45%" 같은 텍스트 표시용

    private void Update()
    {
        // 서버에 연결되지 않았거나 GameMaster가 없으면 UI 갱신 중지
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || GameMaster.Instance == null)
            return;

        var economy = GameMaster.Instance.economyManager;
        var dayCycle = GameMaster.Instance.dayCycleManager;

        // --- 데이터 가져오기 ---
        int loanLimit = economy.availableLoanLimit.Value;
        int currentQuota = economy.dynamicWeeklyQuota.Value;
        int dailyMin = economy.GetDailyMinimumRequired(dayCycle.currentDayIndex.Value);
        int repaidAmount = economy.accumulatedRepayment.Value;

        // 1. 대출 가능 금액
        loanLimitText.text = $"{loanLimit:N0} G"; // N0는 천 단위 콤마(,)를 찍어줍니다.

        // 2. 주간 할당량
        weeklyQuotaText.text = $"{currentQuota:N0} G";

        // 3. 일일 할당량 (오늘 당장 못 갚으면 이자 붙는 금액)
        dailyMinimumText.text = $"{dailyMin:N0} G";

        // 4. 달성 정도 (0.0 ~ 1.0 계산)
        // 할당량이 0일 때의 0 나누기 오류 방지
        float progressValue = currentQuota > 0 ? (float)repaidAmount / currentQuota : 0f;

        // 게이지 바 채우기 (0.0 ~ 1.0)
        if (quotaProgressBar != null)
        {
            quotaProgressBar.fillAmount = progressValue;
        }

        // 퍼센트 텍스트 업데이트
        if (progressPercentText != null)
        {
            progressPercentText.text = $"{(progressValue * 100):F1}%"; // 45.2% 형태로 출력
        }
    }
}