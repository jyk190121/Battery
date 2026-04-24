using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class QuestDescriptionPanelController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("슬롯 인덱스")]
    public int slotIndex;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(!IsLocalControlOwner()) return; // 로컬 클라이언트가 점유자가 아닐 때는 서버 RPC 호출하지 않음

        if(QuestManager.Instance == null || slotIndex >= QuestManager.Instance.activeQuests.Count) return; // 퀘스트 매니저가 없거나 인덱스가 범위를 초과하면 실행하지 않음

        if (TabletUIManager.Instance != null)
        {
            TabletUIManager.Instance.SetHoveredQuestIndexServerRpc(slotIndex);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if(!IsLocalControlOwner()) return; 

        if (TabletUIManager.Instance != null)
        {
            TabletUIManager.Instance.SetHoveredQuestIndexServerRpc(-1); // -1은 아무것도 선택되지 않은 상태를 나타냄
        }
    }

    private bool IsLocalControlOwner()
    {
        if (TabletUIManager.Instance == null) return false;

        // 현재 로컬 클라이언트 ID와 태블릿 점유자 ID가 일치하는지 확인
        ulong myId = NetworkManager.Singleton.LocalClientId;
        return TabletUIManager.Instance.currentTabletUser.Value == myId;
    }
}