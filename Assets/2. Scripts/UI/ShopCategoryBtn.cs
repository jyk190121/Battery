using UnityEngine;
using UnityEngine.UI;

public class ShopCategoryBtn : MonoBehaviour
{
    public GameObject ShopCategoryPanel;

    public void OnChangeCategory()
    {
        if (ShopCategoryPanel.activeSelf) return;
        ShopCategoryPanel.SetActive(!ShopCategoryPanel.activeSelf);
    }

    public void OnChangePanel()
    {
        ShopCategoryPanel.SetActive(!ShopCategoryPanel.activeSelf);
    }
}
