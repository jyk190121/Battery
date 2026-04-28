using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "QuestSystem/QuestData")]
public class QuestDataSO : ScriptableObject
{
    public int questID;
    public string questName;
    [TextArea] public string description;
    public QuestType type;

    [Header("Reward Settings")]
    public int baseReward;
    public float bonusMultiplier;
    public bool isHazardQuest;

    [Header("Target Settings")]
    public int targetItemID;

    [Header("Generation Logic")]
    [Range(1, 10)]
    public int questLevel; // 1~3(초급), 4~7(노말), 8~10(하드) 판정용

    public string targetType;
}

public enum QuestType { Collect, Return, Photo, Record }
public enum QuestDifficulty { Easy, Normal, Hard, Extraction }