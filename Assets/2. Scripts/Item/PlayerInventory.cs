using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode; // NetworkBehaviour 사용을 위한 네임스페이스 추가

public class PlayerInventory : NetworkBehaviour // MonoBehaviour에서 변경
{
    // 전역에서 접근 가능한 로컬 인스턴스 (선택 사항)
    public static PlayerInventory LocalInstance { get; private set; }

    [Header("Inventory Slots")]
    public ItemBase[] slots = new ItemBase[4];
    public int currentSlotIndex = 0;
    [HideInInspector] public ItemBase twoHandedItem = null;

    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask itemLayer;

    // [핵심 변경] 인스펙터 할당 대신 이름으로 찾기 위한 문자열
    [Header("Hand Transform Names (자식 오브젝트 이름)")]
    public string leftHandName = "OneHandle";   // 실제 프리팹 안의 오브젝트 이름과 맞추세요
    public string bothHandsName = "BothHandle";

    // 인스펙터에서 숨기고 코드에서 동적으로 할당합니다.
    [HideInInspector] public Transform leftHandTransform;
    [HideInInspector] public Transform bothHandsTransform;

    public float throwForce = 7f;

    public Action<int> OnSlotChanged;
    public Action OnInventoryUpdated;
    public Action<bool> OnTwoHandedToggled;

    private ItemBase lastLookedItem;
    private DepartureButton lastLookedButton;

    // [핵심 변경] Start() 대신 NGO 전용 스폰 콜백 사용
    public override void OnNetworkSpawn()
    {
        // 1. 내 캐릭터, 남의 캐릭터 상관없이 손 위치는 무조건 찾아야 합니다.
        leftHandTransform = FindChildByName(transform, leftHandName);
        bothHandsTransform = FindChildByName(transform, bothHandsName);

        if (leftHandTransform == null || bothHandsTransform == null)
        {
            Debug.LogError($"[PlayerInventory] 손 Transform을 찾지 못했습니다! 자식 오브젝트 이름을 확인해주세요.");
        }

        // 2. 내 조종 권한이 있는 캐릭터일 때만 로직 실행 및 데이터 복구
        if (IsOwner)
        {
            LocalInstance = this;
            LoadInventoryData();
        }
    }

    // [핵심 변경] 이름으로 자식 오브젝트를 깊이 탐색하여 찾는 재귀 함수
    private Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName) return child;
        }
        return null;
    }

    void Update()
    {
        // [핵심 변경] 내가 조종하는 캐릭터가 아니면 레이캐스트 및 키보드 입력 무시
        if (!IsOwner) return;

        CheckInteraction();
        HandleSlotChange();

        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                // 양손 템을 들고 있어도 버튼(이륙/출발) 상호작용은 최우선으로 가능하게 처리
                if (lastLookedButton != null)
                {
                    lastLookedButton.Interact(this);
                }
                else if (lastLookedItem != null)
                {
                    TryPickUpAction();
                }
            }
            if (Keyboard.current[Key.G].wasPressedThisFrame) RequestDropCurrentItem();
        }
    }

    private void CheckInteraction()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, itemLayer))
        {
            if (hit.collider.TryGetComponent(out ItemBase targetItem))
            {
                if (lastLookedItem != targetItem)
                {
                    ClearHighlight();
                    lastLookedItem = targetItem;
                    Debug.Log($"포커스: {targetItem.itemData.itemName}");
                    if (lastLookedItem.TryGetComponent(out Outline outline)) outline.enabled = true;
                }
                return;
            }
            if (hit.collider.TryGetComponent(out DepartureButton targetButton))
            {
                if (lastLookedButton != targetButton)
                {
                    ClearHighlight();
                    lastLookedButton = targetButton;
                    Debug.Log("<color=magenta>이륙 버튼 조준됨.</color>");
                }
                return;
            }
        }
        ClearHighlight();
    }

    private void ClearHighlight()
    {
        if (lastLookedItem != null)
        {
            if (lastLookedItem.TryGetComponent(out Outline outline)) outline.enabled = false;
            lastLookedItem = null;
        }
        lastLookedButton = null;
    }

    private void TryPickUpAction()
    {
        if (lastLookedItem != null) LocalPickUpLogic(lastLookedItem);
    }

    private void LocalPickUpLogic(ItemBase targetItem)
    {
        if (targetItem.TryGetComponent(out Outline outline)) outline.enabled = false;
        lastLookedItem = null;

        // 1. [양손 룰] 이미 양손 아이템을 들고 있다면 다른 아이템 줍기 절대 불가
        if (twoHandedItem != null)
        {
            Debug.Log("<color=red>양손을 사용 중이라 다른 아이템을 주울 수 없습니다!</color>");
            return;
        }

        // 2. 빈 슬롯 찾기 (현재 슬롯 우선 -> 나머지 앞에서부터)
        int emptySlotIndex = -1;
        if (slots[currentSlotIndex] == null)
        {
            emptySlotIndex = currentSlotIndex;
        }
        else
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) { emptySlotIndex = i; break; }
            }
        }

        // 3. [한손 룰 & 양손 룰] 빈 슬롯이 없으면 획득 불가 (스왑 없음)
        if (emptySlotIndex == -1)
        {
            Debug.Log("<color=red>인벤토리가 가득 찼습니다! (G키로 버려야 합니다)</color>");
            return;
        }

        // --- 무조건 빈칸이 1개 이상 있음 ---

        if (targetItem.itemData.handType == HandType.TwoHand)
        {
            // 양손 아이템도 일반 슬롯 1칸을 점유함
            slots[emptySlotIndex] = targetItem;
            twoHandedItem = targetItem;

            // 한손 아이템을 들고 있었다면 모델링 숨기기 (가방에 넣는 연출)
            if (slots[currentSlotIndex] != null && slots[currentSlotIndex] != targetItem)
            {
                slots[currentSlotIndex].gameObject.SetActive(false);
            }

            targetItem.RequestChangeOwnership(true, bothHandsTransform);
            OnTwoHandedToggled?.Invoke(true);
        }
        else // 한손 아이템
        {
            slots[emptySlotIndex] = targetItem;
            targetItem.RequestChangeOwnership(true, leftHandTransform);

            // 주운 빈칸이 현재 들고 있는 슬롯이 아니라면 바로 가방에 넣음(안 보이게)
            if (emptySlotIndex != currentSlotIndex)
            {
                targetItem.gameObject.SetActive(false);
            }
        }

        OnInventoryUpdated?.Invoke();
    }

    public void RequestDropCurrentItem()
    {
        ItemBase itemToDrop = null;

        if (twoHandedItem != null)
        {
            itemToDrop = twoHandedItem;

            // 배열을 뒤져서 양손 아이템이 차지하고 있던 슬롯을 비움
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == twoHandedItem)
                {
                    slots[i] = null;
                    break;
                }
            }

            twoHandedItem = null;
            OnTwoHandedToggled?.Invoke(false);

            // 양손 템을 버렸으니, 원래 들고 있던 현재 슬롯의 한손템을 다시 꺼냄
            if (slots[currentSlotIndex] != null)
            {
                slots[currentSlotIndex].gameObject.SetActive(true);
            }
        }
        else if (slots[currentSlotIndex] != null)
        {
            itemToDrop = slots[currentSlotIndex];
            slots[currentSlotIndex] = null;
        }

        // 투척 물리 연산
        if (itemToDrop != null)
        {
            itemToDrop.RequestChangeOwnership(false, null);
            itemToDrop.transform.position = Camera.main.transform.position + Camera.main.transform.forward;
            if (itemToDrop.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce((Camera.main.transform.forward + Vector3.up * 0.2f) * throwForce, ForceMode.Impulse);
                itemToDrop.BeginThrownState();
            }
            OnInventoryUpdated?.Invoke();
        }
    }

    private void HandleSlotChange()
    {
        // [양손 룰] 양손 아이템을 들고 있다면 마우스 휠(슬롯 변경) 강제 잠금
        if (twoHandedItem != null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;

        int prev = currentSlotIndex;

        if (scroll < 0f && currentSlotIndex < slots.Length - 1) currentSlotIndex++;
        else if (scroll > 0f && currentSlotIndex > 0) currentSlotIndex--;

        if (prev != currentSlotIndex)
        {
            if (slots[prev] != null) slots[prev].gameObject.SetActive(false);
            if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(true);
            OnSlotChanged?.Invoke(currentSlotIndex);
        }
    }

    private void LoadInventoryData()
    {
        if (GameSessionManager.Instance == null || GameSessionManager.Instance.playerItems.Count == 0) return;

        Debug.Log($"<color=orange><b>[Inventory]</b> {GameSessionManager.Instance.playerItems.Count}개의 인벤토리 아이템 복구를 시작합니다.</color>");

        foreach (var data in GameSessionManager.Instance.playerItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(data.itemID);
            if (prefab == null)
            {
                Debug.LogError($"🚨 프리팹 DB에서 ID {data.itemID}를 찾을 수 없습니다!");
                continue;
            }

            ItemBase spawned = Instantiate(prefab);

            if (spawned is Item_Durability dur)
            {
                dur.currentDurability = data.stateValues[0];
            }

            slots[data.slotIndex] = spawned;

            // 양손/한손 타입에 맞춰서 쥐어주기
            if (spawned.itemData.handType == HandType.TwoHand)
            {
                twoHandedItem = spawned;
                spawned.RequestChangeOwnership(true, bothHandsTransform);
                OnTwoHandedToggled?.Invoke(true);
            }
            else
            {
                spawned.RequestChangeOwnership(true, leftHandTransform);
                // 양손 템이 없을 때만 다른 슬롯 템을 숨김
                if (data.slotIndex != currentSlotIndex && twoHandedItem == null)
                {
                    spawned.gameObject.SetActive(false);
                }
            }

            // 만약 양손템을 들고 씬을 넘었다면, 복구된 한손템들은 전부 가려야 함
            if (twoHandedItem != null && spawned != twoHandedItem)
            {
                spawned.gameObject.SetActive(false);
            }
        }

        OnInventoryUpdated?.Invoke();
        OnSlotChanged?.Invoke(currentSlotIndex);

        Debug.Log("<color=orange><b>[Inventory]</b> 인벤토리 UI 및 아이템 복구 완료.</color>");
    }
}