using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "QuestSystem/QuestData")]
public class QuestDataSO : ScriptableObject
{
    public int questID;
    public string questName;
    [TextArea] public string description;
    public QuestType type;
    public QuestDifficulty difficulty;

    [Header("Reward Settings")]
    public int baseReward;
    public float bonusMultiplier;
    public bool isHazardQuest;

    [Header("Target Settings")]
    public int targetItemID;
}

public enum QuestType { Collect, Return, Photo, Explore, Record }
public enum QuestDifficulty { Easy, Normal, Hard, Extraction }