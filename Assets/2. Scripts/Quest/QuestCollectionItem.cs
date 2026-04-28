using UnityEngine;

public class QuestCollectionItem : ItemBase
{
    //에러 해결을 위해 추가 (수집 퀘스트 실시간 피드백용 소유권 추적)
    [Header("Quest Tracking")]
    public ulong lastHolderId;

    protected override void Start()
    {
        base.Start();
        // 퀘스트 아이템은 특별한 동작이 없으므로 Base 로직만 탑재합니다.
        // 나중에 퀘스트 아이템 전용 연출(빛나기 등)이 필요하면 여기에 추가합니다.
    }
}
