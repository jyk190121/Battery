using UnityEngine;
using TMPro; // TextMeshPro 사용

public class QuestPanelUI : MonoBehaviour
{
    private QuestDataSO m_QuestDataSO;

    [Header("UI 연결")]
    public TextMeshProUGUI questNameText;
    public TextMeshProUGUI questRewardText;

    // 그룹 스크립트에서 호출하여 데이터를 주입해 줄 함수
    public void Setup(QuestDataSO data)
    {
        m_QuestDataSO = data;

        // UI에 텍스트 반영
        if (m_QuestDataSO != null)
        {
            if (questNameText != null) questNameText.text = m_QuestDataSO.questName;
            if (questRewardText != null) questRewardText.text = m_QuestDataSO.baseReward.ToString();
        }
    }
}