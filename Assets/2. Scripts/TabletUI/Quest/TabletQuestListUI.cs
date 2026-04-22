using UnityEngine;

public class TabletQuestListUI : MonoBehaviour
{
    [Header("난이도별 그룹 패널 연결")]
    // 그룹 스크립트를 연결합니다.
    public QuestDifficultyGroupUI easyGroup;
    public QuestDifficultyGroupUI normalGroup;
    public QuestDifficultyGroupUI hardGroup;

    // UI(태블릿)가 화면에 켜질 때마다 최신화
    private void OnEnable()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (QuestManager.Instance != null)
        {
            // 각 그룹 패널에 '퀘스트 ID 리스트'와 '자신의 난이도'를 함께 넘겨줌
            easyGroup.SetupGroup(QuestManager.Instance.easyOffered, QuestDifficulty.Easy);
            normalGroup.SetupGroup(QuestManager.Instance.normalOffered, QuestDifficulty.Normal);
            hardGroup.SetupGroup(QuestManager.Instance.hardOffered, QuestDifficulty.Hard);
        }
    }
}