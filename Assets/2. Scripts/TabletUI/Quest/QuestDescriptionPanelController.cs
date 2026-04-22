using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class QuestDescriptionPanelController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 연결")]
    public GameObject DescriptionPanel;

    private void Start()
    {
        if (DescriptionPanel != null)
        {
            DescriptionPanel.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 현재 퀘스트 매니저에 수락된 퀘스트가 1개라도 있을 때만 설명창을 띄움
        if (DescriptionPanel != null && QuestManager.Instance.activeQuests.Count > 0)
        {
            DescriptionPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (DescriptionPanel != null)
        {
            DescriptionPanel.SetActive(false);
        }
    }
}