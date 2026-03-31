// 파일 이름: ItemDataSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewItemData", menuName = "Item System/Item Data")]
public class ItemDataSO : ScriptableObject
{
    public int itemID;
    public string itemName;
    public ItemCategory category;
    public HandType handType;
    public int basePrice;
    public Sprite icon;
}