using UnityEngine;

// 문을 여는 기능은 PlayerInteraction과 DoorController가 처리하므로,
// 열쇠는 기본 물리/네트워크 기능(ItemBase)만 상속받으면 완벽히 작동합니다.
public class Item_Key : ItemBase
{
    protected override void Start()
    {
        base.Start();
    }
}