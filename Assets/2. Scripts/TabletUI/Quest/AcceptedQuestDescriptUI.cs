using UnityEngine;
using TMPro;

public class AcceptedQuestDescriptUI : MonoBehaviour
{
    public int questIndex;

    [Header("UI Composition")]
    public GameObject[] difficulty;
    public TextMeshProUGUI QuestName;
    public TextMeshProUGUI QuestDescription;
    public TextMeshProUGUI QuestReward;

    private void OnEnable()
    {
        SetUp();
    }

    public void SetUp()
    {
        if (QuestManager.Instance == null || questIndex >= QuestManager.Instance.activeQuests.Count)
            return;

        int id = QuestManager.Instance.activeQuests[questIndex];
        QuestDataSO data = QuestManager.Instance.GetQuestData(id);

        if (data != null)
        {
            QuestName.text = data.questName;
            QuestDescription.text = data.description;
            QuestReward.text = data.baseReward.ToString();

            // 모든 난이도 비활성화 후 선택된 난이도만 켜기
            foreach (var icon in difficulty) if (icon != null) icon.SetActive(false);

            // QuestManager의 selectedDifficulty.Value (NetworkVariable)를 참조
            int diffIdx = (int)QuestManager.Instance.selectedDifficulty.Value;
            if (diffIdx < difficulty.Length && difficulty[diffIdx] != null)
            {
                difficulty[diffIdx].SetActive(true);
            }
        }
    }
}