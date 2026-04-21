using UnityEngine;
using UnityEngine.UI;

public class TabletButton : MonoBehaviour
{
    [Header("퀘스트 UI")]
    public Button questOpenButton;      // 메인 화면의 Quest 아이콘 버튼
    public GameObject questPanel;       // 퀘스트 화면 패널

    [Header("상점 UI")]
    public Button shopOpenButton;       // 메인 화면의 Shop 아이콘 버튼
    public GameObject shopPanel;        // 상점 화면 패널

    public Button closePanelButton;     // 상점/퀘스트 패널 끄는 버튼

    private void Start()
    {
        // 퀘스트 버튼 이벤트 자동 연결
        if (questOpenButton != null) questOpenButton.onClick.AddListener(OpenQuest);

        // 상점 버튼 이벤트 자동 연결
        if (shopOpenButton != null) shopOpenButton.onClick.AddListener(OpenShop);

        // 닫기 버튼 연결
        if (closePanelButton != null) closePanelButton.onClick.AddListener(ClosePanel);
    }

    // ================= 퀘스트 패널 제어 =================
    public void OpenQuest()
    {
        if (questPanel != null) questPanel.SetActive(true);
    }


    // ================= 상점 패널 제어 =================
    public void OpenShop()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        questPanel.SetActive(false);
        shopPanel.SetActive(false);
    }
}