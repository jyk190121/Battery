using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 사망한 플레이어가 드랍하는 핸드폰 아이템입니다. 원래 주인의 식별 ID를 보관합니다.
/// </summary>
public class Item_Phone : ItemBase
{
    [Header("Phone Data")]
    [Tooltip("이 핸드폰의 원래 주인(사망자)의 Client ID")]
    public ulong originalOwnerId;

    protected override void Start()
    {
        base.Start();
    }
}