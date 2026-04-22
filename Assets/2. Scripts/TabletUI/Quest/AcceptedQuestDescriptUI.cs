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

    void SetUp()
    {
        // 💡 1. 방어 코드: 리스트가 비어있거나 인덱스를 초과하면 실행하지 않음
        if (QuestManager.Instance == null || questIndex >= QuestManager.Instance.activeQuests.Count)
            return;

        int id = QuestManager.Instance.activeQuests[questIndex]; // index 변수명을 id로 변경하여 덜 헷갈리게 함
        QuestDataSO data = QuestManager.Instance.GetQuestData(id);

        if (data != null)
        {
            // 2. data.name -> data.questName 으로 수정
            QuestName.text = data.questName;
            QuestDescription.text = data.description;
            QuestReward.text = data.baseReward.ToString();

            // 3. 기존에 켜져있던 난이도 아이콘들을 모두 끄고 시작 (잔상 방지)
            foreach (var icon in difficulty)
            {
                if (icon != null) icon.SetActive(false);
            }

            switch (QuestManager.Instance.selectedDifficulty)
            {
                case QuestDifficulty.Easy:
                    difficulty[0].SetActive(true);
                    break;
                case QuestDifficulty.Normal:
                    difficulty[1].SetActive(true);
                    break;
                case QuestDifficulty.Hard:
                    difficulty[2].SetActive(true);
                    break;
            }
        }
    }
}