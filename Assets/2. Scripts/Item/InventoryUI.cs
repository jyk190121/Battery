using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    public Image[] slotIcons = new Image[4];      // ItemIcon_1~4 드래그 앤 드롭
    public GameObject[] highlightBox = new GameObject[4]; // ItemSlot_1~4 드래그 앤 드롭

    void Start()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();

            if (playerInventory == null)
            {
                Debug.LogError("🚨 씬에 PlayerInventory를 가진 오브젝트가 없습니다!");
                return; // 플레이어가 없으면 아래 코드를 실행하지 않고 중단
            }
        }

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

    private void HandleTwoHandedUI(bool isHeavy)
    {
        // 양손 아이템 들면 UI 전체를 약간 어둡게 처리
        GetComponent<CanvasGroup>().alpha = isHeavy ? 0.5f : 1.0f;
    }
}