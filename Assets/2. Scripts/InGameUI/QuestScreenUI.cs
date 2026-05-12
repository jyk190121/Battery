using TMPro;
using UnityEngine;

public class QuestScreenUI : MonoBehaviour
{
    [Header("3D 오브젝트용 (이름 + 포인트 + 보상 + 설명)")]
    public TextMeshPro[] QuestList = new TextMeshPro[4];

    [Header("Canvas UI용 (이름 + 포인트만)")]
    public TextMeshProUGUI[] QuestListUI = new TextMeshProUGUI[4];

    private void Start()
    {
        SetUp();
    }

    private void OnEnable()
    {
        SetUp();
    }
    private void SetUp()
    {
        // 1. 퀘스트 매니저 방어 코드
        if (QuestManager.Instance == null || QuestManager.Instance.activeQuests == null)
            return;

        int index = QuestManager.Instance.activeQuests.Count;

        for (int i = 0; i < 4; i++)
        {
            if (i < index)
            {
                int questID = QuestManager.Instance.activeQuests[i];
                QuestDataSO data = QuestManager.Instance.GetQuestData(questID);

                if (data != null)
                {
                    // 2. 타입별로 들어갈 문자열을 다르게 생성
                    // 3D용: 전체 정보 포함
                    string fullText = $"Name: {data.questName}       {data.performancePoint}P   {data.baseReward}G \n {data.description}";

                    // UI용: 이름과 포인트만 포함
                    string shortText = $"{data.questName} \n {data.performancePoint}P  {data.baseReward}G";

                    // 3. 각 배열에 값이 있을 때만 해당 형식의 텍스트 대입
                    if (i < QuestList.Length && QuestList[i] != null)
                    {
                        QuestList[i].text = fullText;
                    }

                    if (i < QuestListUI.Length && QuestListUI[i] != null)
                    {
                        QuestListUI[i].text = shortText;
                    }
                }
            }
            else
            {
                // 데이터가 없는 빈 칸은 양쪽 다 비워줌
                if (i < QuestList.Length && QuestList[i] != null) QuestList[i].text = "";
                if (i < QuestListUI.Length && QuestListUI[i] != null) QuestListUI[i].text = "";
            }
        }
    }
}
