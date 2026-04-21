using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    public Image[] slotIcons = new Image[4];      // ItemIcon_1~4 드래그 앤 드롭
    public GameObject[] highlightBox = new GameObject[4]; // ItemSlot_1~4 드래그 앤 드롭

    void Start()
    {
        StartCoroutine(BindLocalPlayerRoutine());
        TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;
    }

    private System.Collections.IEnumerator BindLocalPlayerRoutine()
    {
        // 💡 [핵심] '나(로컬 클라이언트)'의 인벤토리가 스폰될 때까지 대기
        while (PlayerInventory.LocalInstance == null)
        {
            yield return null;
        }

        playerInventory = PlayerInventory.LocalInstance;

        playerInventory.OnInventoryUpdated += UpdateUI;
        playerInventory.OnSlotChanged += UpdateHighlight;
        playerInventory.OnTwoHandedToggled += HandleTwoHandedUI;

        UpdateUI();
        UpdateHighlight(playerInventory.currentSlotIndex);
    }

    private void UpdateUI()
    {
        for (int i = 0; i < slotIcons.Length; i++)
        {
            ItemBase item = playerInventory.slots[i];
            if (item != null)
            {
                slotIcons[i].sprite = item.itemData.icon;
                slotIcons[i].enabled = true;
            }
            else
            {
                slotIcons[i].enabled = false;
            }
        }
    }

    private void UpdateHighlight(int index)
    {
        for (int i = 0; i < highlightBox.Length; i++)
        {
            Image slotImg = highlightBox[i].GetComponent<Image>();
            if (slotImg != null)
                slotImg.color = (i == index) ? Color.white : new Color(1, 1, 1, 0.3f);
        }
    }

    //태블릿 상태에 따라 UI 투명도 조절 함수
    private void HandleTabletStateChanged(bool isTabletOpen)
    {
        if (TryGetComponent(out CanvasGroup cg))
        {
            cg.alpha = isTabletOpen ? 0f : (PlayerInventory.IsHoldingTwoHanded ? 0.5f : 1.0f);
            cg.interactable = !isTabletOpen;
            cg.blocksRaycasts = !isTabletOpen;
        }
    }
    private void HandleTwoHandedUI(bool isHeavy)
    {
        // 양손 아이템 들면 UI 전체를 약간 어둡게 처리
        GetComponent<CanvasGroup>().alpha = isHeavy ? 0.5f : 1.0f;
    }

    private void OnDestroy()
    {
        // 스크립트가 파괴될 때(씬 이동 등) 연결된 이벤트를 모두 끊어줍니다.
        if (playerInventory != null)
        {
            playerInventory.OnInventoryUpdated -= UpdateUI;
            playerInventory.OnSlotChanged -= UpdateHighlight;
            playerInventory.OnTwoHandedToggled -= HandleTwoHandedUI;
        }
        TabletUIManager.OnTabletStateChanged -= HandleTabletStateChanged;
    }
}