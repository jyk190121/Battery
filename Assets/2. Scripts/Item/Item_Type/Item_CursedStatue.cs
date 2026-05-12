using UnityEngine;

/// <summary>
/// 저주받은 흉상 전용 스크립트
/// 모든 저주 디버프 연산은 PlayerInventory.cs에서 SO 데이터를 읽어 자동 처리한다.
/// </summary>
public class Item_CursedStatue : ItemBase
{
    protected override void Start()
    {
        base.Start();
        // 흉상 고유의 시각적 연출이나 추가 기믹이 필요해지면 이곳에 작성한다.
    }
}