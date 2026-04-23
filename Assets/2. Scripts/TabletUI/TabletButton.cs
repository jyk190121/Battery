using UnityEngine;
using UnityEngine.UI;

public class TabletButton : MonoBehaviour
{
    [Header("퀘스트 UI")]
    public Button questOpenButton;      // 메인 화면의 Quest 아이콘 버튼

    [Header("상점 UI")]
    public Button shopOpenButton;       // 메인 화면의 Shop 아이콘 버튼

    public Button closePanelButton;     // 상점/퀘스트 패널 끄는 버튼

    private void Start()
    {
        // 퀘스트 버튼 이벤트 자동 연결
        if (questOpenButton != null) questOpenButton.onClick.AddListener(OpenQuest);

        // 상점 버튼 이벤트 자동 연결
        if (shopOpenButton != null) shopOpenButton.onClick.AddListener(OpenShop);

        // 닫기 버튼 연결
        if (closePanelButton != null) closePanelButton.onClick.AddListener(ReturnToMain);
    }

    // ================= 퀘스트 패널 제어 =================
    public void OpenQuest()
    {
        if(TabletUIManager.Instance != null)
        {
            TabletUIManager.Instance.RequestScreenChangeServerRpc(TVScreenState.QUEST);
        }
    }


    // ================= 상점 패널 제어 =================
    public void OpenShop()
    {
        if(TabletUIManager.Instance != null)
        {
            TabletUIManager.Instance.RequestScreenChangeServerRpc(TVScreenState.SHOP_CONSUME);
        }
    }

    public void ReturnToMain()
    {
        if(TabletUIManager.Instance != null)
        {
            TabletUIManager.Instance.RequestScreenChangeServerRpc(TVScreenState.MAIN);
        }
    }
}