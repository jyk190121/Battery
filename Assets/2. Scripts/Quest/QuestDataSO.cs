using UnityEngine;

public enum QuestType { Collect, Return, Photo, Record }
public enum QuestDifficulty { Easy, Normal, Hard, Extraction }

[CreateAssetMenu(fileName = "NewQuest", menuName = "QuestSystem/QuestData")]
public class QuestDataSO : ScriptableObject
{
    public int questID;
    public string questName;
    [TextArea] public string description;
    public QuestType type;
    public QuestDifficulty difficulty;

    [Header("Target Settings")]
    public string targetType;

    [Header("Reward Settings")]
    public int baseReward;
    public float bonusMultiplier;
    public bool isHazardQuest;
    public int performancePoint;

    [Header("Collection Gimmick")]
    public int targetItemID;        // 수집/환원 대상 아이템 ID
    public int passwordCount;       // 비밀번호 자리수 (2, 3, 4)
    public int materialCount;       // 발전기 재료 수 (2, 3, 4)

    [Header("Item Effects (Collect Type)")]
    public bool hasSpeedDebuff;     // 이동속도 -25%
    public bool hasMonsterAggro;    // 몬스터 어그로 사운드[cite: 1]
    public bool hasHallucination;   // 환청 효과[cite: 1]

}
