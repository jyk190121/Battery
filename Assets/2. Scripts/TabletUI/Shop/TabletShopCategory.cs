using UnityEngine;
using UnityEngine.UI;

public class TabletShopCategory : MonoBehaviour
{
    // 패널 모음
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
        if(TabletUIManager.Instance != null)
        {
            switch (PanelNumber)
            {
                case 0:
                    TabletUIManager.Instance.RequestScreenChangeServerRpc(TVScreenState.SHOP_CONSUME);
                    break;
                case 1:
                    TabletUIManager.Instance.RequestScreenChangeServerRpc(TVScreenState.SHOP_DURABLE);
                    break;
                case 2:
                    TabletUIManager.Instance.RequestScreenChangeServerRpc(TVScreenState.SHOP_STAT);
                    break;
                case 3:
                    TabletUIManager.Instance.RequestScreenChangeServerRpc(TVScreenState.SHOP_WEAPON);
                    break;
            }
        }
    }
}
