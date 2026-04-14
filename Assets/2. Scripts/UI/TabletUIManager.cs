using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class TabletUIManager : MonoBehaviour
{
    [Header("UI Parent Panel")]
    // 중요: 스크립트가 붙은 오브젝트 자체를 끄지 말고, 
    // 실제 UI 요소들이 담긴 자식 Panel 오브젝트를 여기에 연결하세요.
    public GameObject tabletUIPanel;

    [Header("UI 텍스트 연결")]
    public TextMeshProUGUI loanLimitText;
    public TextMeshProUGUI weeklyQuotaText;
    public TextMeshProUGUI dailyMinimumText;

    [Header("UI 프로그레스 바 연결")]
    public Image quotaProgressBar;
    public TextMeshProUGUI progressPercentText;

    private void Start()
    {
        // 처음 시작 시 UI 패널만 숨깁니다. (스크립트는 계속 살아있음)
        if (tabletUIPanel != null)
            tabletUIPanel.SetActive(false);

        ResetUI();
    }

    private void Update()
    {
        // UI가 켜져 있을 때만 ESC 키 체크 및 데이터 갱신
        if (tabletUIPanel == null || !tabletUIPanel.activeSelf) return;

        RefreshData();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTabletUI();
        }
    }

    // 외부(PlayerInteraction)에서 호출할 함수
    public void OpenTabletUI()
    {
        if (tabletUIPanel != null)
        {
            tabletUIPanel.SetActive(true);
            RefreshData(); // 켜지는 순간 즉시 데이터 동기화

            // 마우스 커서 활성화 (필요 시)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void CloseTabletUI()
    {
        if (tabletUIPanel != null)
        {
            tabletUIPanel.SetActive(false);

            // 마우스 커서 다시 잠금 (필요 시)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void RefreshData()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || GameMaster.Instance == null)
            return;

        var economy = GameMaster.Instance.economyManager;
        var dayCycle = GameMaster.Instance.dayCycleManager;

        if (economy == null || dayCycle == null) return;

        // 데이터 계산 및 할당
        int loanLimit = economy.availableLoanLimit.Value;
        int currentQuota = economy.dynamicWeeklyQuota.Value;
        int dailyMin = economy.GetDailyMinimumRequired(dayCycle.currentDayIndex.Value);
        int repaidAmount = economy.accumulatedRepayment.Value;

        if (loanLimitText != null) loanLimitText.text = $"{loanLimit:N0} G";
        if (weeklyQuotaText != null) weeklyQuotaText.text = $"{currentQuota:N0} G";
        if (dailyMinimumText != null) dailyMinimumText.text = $"{dailyMin:N0} G";

        float progressValue = currentQuota > 0 ? (float)repaidAmount / currentQuota : 0f;

        if (quotaProgressBar != null) quotaProgressBar.fillAmount = progressValue;
        if (progressPercentText != null) progressPercentText.text = $"{(progressValue * 100):F1}%";
    }

    private void ResetUI()
    {
        if (loanLimitText != null) loanLimitText.text = "0 G";
        if (weeklyQuotaText != null) weeklyQuotaText.text = "0 G";
        if (dailyMinimumText != null) dailyMinimumText.text = "0 G";
        if (progressPercentText != null) progressPercentText.text = "0%";
        if (quotaProgressBar != null) quotaProgressBar.fillAmount = 0f;
    }
}