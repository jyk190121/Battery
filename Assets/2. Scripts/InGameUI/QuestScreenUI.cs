using TMPro;
using UnityEngine;

public class QuestScreenUI : MonoBehaviour
{
    public TextMeshPro[] QuestList = new TextMeshPro[4];

    private void Start()
    {
        // 1. 퀘스트 매니저가 없거나 아직 준비되지 않았으면 에러 없이 중단 (튕김 방지)
        if (QuestManager.Instance == null || QuestManager.Instance.activeQuests == null)
            return;

        int index = QuestManager.Instance.activeQuests.Count;

        for (int i = 0; i < QuestList.Length; i++)
        {
            if (i < index)
            {
                int questID = QuestManager.Instance.activeQuests[i];
                QuestDataSO data = QuestManager.Instance.GetQuestData(questID);
                if (data != null)
                {
                    QuestList[i].text = $"Name: {data.questName}       {data.performancePoint}P   {data.baseReward}G \n {data.description}";
                }
            }
            else
            {
                QuestList[i].text = "";
            }
        }
    }
}
