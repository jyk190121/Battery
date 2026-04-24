using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class QuestDescriptionPanelController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public int slotIndex; // 퀘스트 슬롯 인덱스

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 점유권을 가진 사람만 서버에 요청 가능
        if (!IsLocalControlOwner()) return;

        // 퀘스트가 있는 슬롯인지 확인 후 RPC 호출
        if (QuestManager.Instance != null && slotIndex < QuestManager.Instance.activeQuests.Count)
        {
            if (TabletUIManager.Instance != null)
                TabletUIManager.Instance.SetHoveredQuestIndexServerRpc(slotIndex);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsLocalControlOwner()) return;

        if (TabletUIManager.Instance != null)
            TabletUIManager.Instance.SetHoveredQuestIndexServerRpc(-1);
    }

    private bool IsLocalControlOwner()
    {
        if (TabletUIManager.Instance == null) return false;
        return TabletUIManager.Instance.currentTabletUser.Value == NetworkManager.Singleton.LocalClientId;
    }
}