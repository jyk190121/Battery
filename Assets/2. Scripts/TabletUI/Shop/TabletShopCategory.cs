using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class TabletShopCategory : MonoBehaviour
{
    // 패널 모음
    public List<GameObject> CategoryPanelList = new List<GameObject>();
    public Button ConsumeBtn;
    public Button DurableBtn;
    public Button StatItemBtn;
    public Button WeaponBtn;

    private void OnEnable()
    {
        ConsumeBtn.onClick.AddListener(() => OpenCategoryPanel(0));
        DurableBtn.onClick.AddListener(() => OpenCategoryPanel(1));
        StatItemBtn.onClick.AddListener(() => OpenCategoryPanel(2));
        WeaponBtn.onClick.AddListener(() => OpenCategoryPanel(3));
    }

    private void OnDisable()
    {
        ConsumeBtn.onClick.RemoveAllListeners();
        DurableBtn.onClick.RemoveAllListeners();
        StatItemBtn.onClick.RemoveAllListeners();
        WeaponBtn.onClick.RemoveAllListeners();
    }

    public void OpenCategoryPanel(int PanelNumber)
    {
        foreach (GameObject categoryPanel in CategoryPanelList)
        {
            categoryPanel.SetActive(false);
        }

        CategoryPanelList[PanelNumber].SetActive(true);
    }
}
