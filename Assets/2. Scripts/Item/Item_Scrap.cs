public class Item_Scrap : ItemBase
{
    public int currentScrapValue;
    private void Start() { if (currentScrapValue == 0) currentScrapValue = itemData.basePrice; }
}