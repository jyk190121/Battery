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
        // 1. QuestManager.Instance.activeQuests.Count 체크 추가하여 인덱스 범위 벗어나는 경우 패널 끄기
        if (QuestManager.Instance == null || questIndex < 0 || questIndex >= QuestManager.Instance.activeQuests.Count)
        {
            gameObject.SetActive(false); // 잘못된 접근 시 패널 끄기
            return;
        }

        int id = QuestManager.Instance.activeQuests[questIndex]; // index 변수명을 id로 변경하여 덜 헷갈리게 함
        QuestDataSO data = QuestManager.Instance.GetQuestData(id);

        if (data != null)
        {
            // 2. data.name -> data.questName 으로 수정
            QuestName.text = data.questName;
            QuestDescription.text = data.description;
            QuestReward.text = data.baseReward.ToString();

            switch (QuestManager.Instance.selectedDifficulty.Value)
            {
                case QuestDifficulty.Easy:
                    if (difficulty.Length > 0 && difficulty[0] != null) difficulty[0].SetActive(true);
                    break;
                case QuestDifficulty.Normal:
                    if (difficulty.Length > 1 && difficulty[1] != null) difficulty[1].SetActive(true);
                    break;
                case QuestDifficulty.Hard:
                    if (difficulty.Length > 2 && difficulty[2] != null) difficulty[2].SetActive(true);
                    break;
            }
        }
    }
}