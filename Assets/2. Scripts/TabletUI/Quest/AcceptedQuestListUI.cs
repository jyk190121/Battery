using UnityEngine;
using TMPro;
using Unity.Netcode;

public class AcceptedQuestListUI : MonoBehaviour
{
    [Header("메인 화면의 퀘스트 이름 텍스트 4개")]
    public TextMeshProUGUI[] acceptedQuestTexts; // 인스펙터에서 4개의 텍스트 연결

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            // activeQuests 리스트에 변화가 생길 때마다 HandleListChanged 함수를 실행하도록 구독
            QuestManager.Instance.activeQuests.OnListChanged += HandleListChanged;

            // 처음 켜졌을 때도 현재 상태를 한 번 반영
            RefreshUI();
        }
    }

    private void OnDestroy()
    {
        // UI가 파괴될 때 이벤트 구독 해제 (메모리 누수 방지)
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.activeQuests.OnListChanged -= HandleListChanged;
        }
    }

    // 리스트가 변경될 때 자동으로 호출되는 함수
    private void HandleListChanged(NetworkListEvent<int> changeEvent)
    {
        RefreshUI();
    }

    // 실제 UI 텍스트를 갱신하는 함수
    private void RefreshUI()
    {
        if (QuestManager.Instance == null) return;

        for (int i = 0; i < acceptedQuestTexts.Length; i++)
        {
            // 현재 수락된 퀘스트 개수 범위 안일 경우 (보통 4개)
            if (i < QuestManager.Instance.activeQuests.Count)
            {
                int questID = QuestManager.Instance.activeQuests[i];
                QuestDataSO data = QuestManager.Instance.GetQuestData(questID);

                if (data != null)
                {
                    acceptedQuestTexts[i].text = data.questName;
                }
            }
            else
            {
                // 아직 수락된 퀘스트가 없거나 모자랄 경우 빈칸 처리
                // "대기 중..." 등 다른 텍스트를 넣어도 좋습니다.
                acceptedQuestTexts[i].text = "";
            }
        }
    }
}