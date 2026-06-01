using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    public Image[] slotIcons = new Image[4];
    public GameObject[] highlightBox = new GameObject[4];

    private void Start()
    {
        StartCoroutine(BindLocalPlayerRoutine());
        TabletUIManager.OnTabletStateChanged += HandleTabletStateChanged;
    }

    private System.Collections.IEnumerator BindLocalPlayerRoutine()
    {
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
        for (int slotIndex = 0; slotIndex < slotIcons.Length; slotIndex++)
        {
            ItemBase item = playerInventory.slots[slotIndex];

            if (item != null)
            {
                slotIcons[slotIndex].sprite = item.itemData.icon;
                slotIcons[slotIndex].enabled = true;
            }
            else
            {
                slotIcons[slotIndex].enabled = false;
            }
        }

        UpdateHighlight(playerInventory.currentSlotIndex);
        HandleTwoHandedUI(PlayerInventory.IsHoldingTwoHanded);
    }

    private void UpdateHighlight(int selectedIndex)
    {
        int targetIndex = selectedIndex;

        if (playerInventory != null && PlayerInventory.IsHoldingTwoHanded)
        {
            for (int slotIndex = 0; slotIndex < playerInventory.slots.Length; slotIndex++)
            {
                if (playerInventory.slots[slotIndex] == playerInventory.twoHandedItem)
                {
                    targetIndex = slotIndex;
                    break;
                }
            }
        }

        for (int boxIndex = 0; boxIndex < highlightBox.Length; boxIndex++)
        {
            Image slotImage = highlightBox[boxIndex].GetComponent<Image>();

            if (slotImage != null)
            {
                slotImage.color = (boxIndex == targetIndex) ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            }
        }

        if (playerInventory != null)
        {
            HandleTwoHandedUI(PlayerInventory.IsHoldingTwoHanded);
        }
    }

    private void HandleTabletStateChanged(bool isTabletOpen)
    {
        if (TryGetComponent(out CanvasGroup canvasGroup))
        {
            bool isHeavy = PlayerInventory.IsHoldingTwoHanded;

            canvasGroup.alpha = isTabletOpen ? 0f : (isHeavy ? 0.5f : 1.0f);
            canvasGroup.interactable = !isTabletOpen;
            canvasGroup.blocksRaycasts = !isTabletOpen;
        }
    }

    private void HandleTwoHandedUI(bool isHeavy)
    {
        if (TryGetComponent(out CanvasGroup canvasGroup))
        {
            canvasGroup.alpha = isHeavy ? 0.5f : 1.0f;
        }
    }

    private void OnDestroy()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryUpdated -= UpdateUI;
            playerInventory.OnSlotChanged -= UpdateHighlight;
            playerInventory.OnTwoHandedToggled -= HandleTwoHandedUI;
        }

        TabletUIManager.OnTabletStateChanged -= HandleTabletStateChanged;
    }
}