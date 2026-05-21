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
        // [핵심] '나(로컬 클라이언트)'의 인벤토리가 스폰될 때까지 대기
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

        HandleTwoHandedUI(PlayerInventory.IsHoldingTwoHanded);
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
        // 아이템을 버리거나 주웠을 때 즉시 무기 판별 갱신
        UpdateHighlight(playerInventory.currentSlotIndex);
        HandleTwoHandedUI(PlayerInventory.IsHoldingTwoHanded);
    }

    private void UpdateHighlight(int index)
    {
        int targetIndex = index;

        // [핵심 기획 추가] 양손 아이템을 들고 있다면, 해당 아이템이 위치한 슬롯 번호로 타겟 변경
        if (playerInventory != null && PlayerInventory.IsHoldingTwoHanded)
        {
            for (int i = 0; i < playerInventory.slots.Length; i++)
            {
                if (playerInventory.slots[i] == playerInventory.twoHandedItem)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        for (int i = 0; i < highlightBox.Length; i++)
        {
            Image slotImg = highlightBox[i].GetComponent<Image>();
            if (slotImg != null)
                slotImg.color = (i == targetIndex) ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        // 슬롯을 바꿨을 때 즉시 무기 판별 갱신
        if (playerInventory != null) HandleTwoHandedUI(PlayerInventory.IsHoldingTwoHanded);
    }

    //태블릿 상태에 따라 UI 투명도 조절 함수
    private void HandleTabletStateChanged(bool isTabletOpen)
    {
        if (TryGetComponent(out CanvasGroup cg))
        {
            // 태블릿 열고 닫을 때도 무기 여부 체크
            bool isHeavy = PlayerInventory.IsHoldingTwoHanded;

            cg.alpha = isTabletOpen ? 0f : (isHeavy ? 0.5f : 1.0f);
            cg.interactable = !isTabletOpen;
            cg.blocksRaycasts = !isTabletOpen;
        }
    }

    private void HandleTwoHandedUI(bool isHeavy)
    {
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