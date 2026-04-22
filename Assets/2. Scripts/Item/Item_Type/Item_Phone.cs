using UnityEngine;
using Unity.Netcode;

public class Item_Phone : ItemBase
{
    [Header("Phone Data")]
    [Tooltip("이 핸드폰의 원래 주인(사망자)의 Client ID")]
    public ulong originalOwnerId;

    protected override void Start()
    {
        base.Start();
        // 폰은 특별한 사용(Use) 모션이 없으므로 Base 로직만 탑재합니다.
    }

    // (선택 사항) 나중에 인벤토리 UI나 상호작용 UI에서 
    // "누구누구의 핸드폰" 이라고 띄워주고 싶을 때 이 ID를 활용하시면 됩니다.
}