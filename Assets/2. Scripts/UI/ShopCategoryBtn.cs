using UnityEngine;
using UnityEngine.UI;

public class ShopCategoryBtn : MonoBehaviour
{
    public GameObject ShopCategoryPanel;

    public void OnChangeCategory()
    {
        ShopCategoryPanel.SetActive(!ShopCategoryPanel.activeSelf);
    }
}
