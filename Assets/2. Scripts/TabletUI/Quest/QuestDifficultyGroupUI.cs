using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI; // NetworkList를 읽기 위해 필요

public class QuestDifficultyGroupUI : MonoBehaviour
{
    [Header("이 그룹에 속한 4개의 퀘스트 패널")]
    public QuestPanelUI[] questPanels; // 인스펙터에서 4개를 연결해주세요.

    private QuestDifficulty m_Difficulty;

    public Button selectBtn;
    public GameObject QuestPanel;

    private void OnEnable()
    {
        selectBtn.onClick.AddListener(OnSelectButtonClicked);
    }

    private void OnDisable()
    {
        selectBtn.onClick.RemoveAllListeners();   
    }

    // 최상위 TabletUI에서 4개짜리 리스트와 난이도를 통째로 넘겨줍니다.
    public void SetupGroup(NetworkList<int> offeredList, QuestDifficulty difficulty)
    {
        m_Difficulty = difficulty;

        // 4개의 자식 패널에 각각 데이터를 쪼개서 분배
        for (int i = 0; i < questPanels.Length; i++)
        {
            if (i < offeredList.Count)
            {
                int questID = offeredList[i];
                QuestDataSO data = QuestManager.Instance.GetQuestData(questID);

                questPanels[i].Setup(data);
                questPanels[i].gameObject.SetActive(true); // 활성화
            }
            else
            {
                // 할당될 퀘스트가 모자라면 패널을 숨김 (안전 장치)
                questPanels[i].gameObject.SetActive(false);
            }
        }
    }

    // 에디터에서 해당 난이도의 'Select' 버튼 OnClick()에 연결할 함수
    public void OnSelectButtonClicked()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.AcceptDifficultyContractServerRpc(m_Difficulty);
            Debug.Log($"<color=green>[UI]</color> {m_Difficulty} 난이도 계약 수락됨!");

            QuestPanel.SetActive(false);
        }
    }
}