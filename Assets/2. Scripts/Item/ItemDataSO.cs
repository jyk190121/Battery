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

public enum ItemCategory { Scrap, Consumable, Durability, Stat, Weapon, Special }
public enum HandType { OneHand, TwoHand }