using UnityEngine;

public class QuestCollectionItem : ItemBase
{

    public static void ApplyDifficultyDebuffs(int tier)
    {
        switch (tier)
        {
            case 1: // ID 1020: Easy (1단계만)
                Debug.Log("<color=yellow>[Tier 1]</color> 속도감소");
                break;

            case 2: // ID 2020: Normal (1~2단계)
                Debug.Log("<color=orange>[Tier 2]</color> 속도감소 + 환청");
                break;

            case 3: // ID 3020: Hard (1~3단계)
                Debug.Log("<color=red>[Tier 3]</color> 속도감소 + 환청 + 어그로");
                break;

            default: // 해당 아이템이 없을 때 (0)
                Debug.Log("<color=white>[Clean]</color> 디버프 해제");
                break;
        }
    }

    protected override void Start()
    {
        base.Start();
        // 퀘스트 아이템은 특별한 동작이 없으므로 Base 로직만 탑재합니다.
        // 나중에 퀘스트 아이템 전용 연출(빛나기 등)이 필요하면 여기에 추가합니다.
    }
}
