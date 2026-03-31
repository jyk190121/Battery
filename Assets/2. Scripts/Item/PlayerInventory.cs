using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Slots")]
    public ItemBase[] slots = new ItemBase[4];
    public int currentSlotIndex = 0;
    public ItemBase twoHandedItem = null;

    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask itemLayer;

    [Header("Hand Transforms")]
    public Transform leftHandTransform;
    public Transform bothHandsTransform;

    [Header("Events")]
    public Action<int> OnSlotChanged;
    public Action OnInventoryUpdated;
    public Action<bool> OnTwoHandedToggled;

    [Header("Status")]
    public float currentWeightPenalty = 1.0f;

    // 조준(Interaction) 관련 변수
    private ItemBase lastLookedItem;

    void Update()
    {
        // 1. 매 프레임 아이템 조준 체크 (외곽선 및 HUD 출력)
        CheckInteraction();

        // 2. 슬롯 변경 (마우스 휠 - 범위 고정형)
        HandleSlotChange();

        // 3. 아이템 줍기 (E키)
        if (Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
        {
            TryPickUpAction();
        }

        // 4. 아이템 버리기 (G키 - 바닥 뚫림 방지 적용)
        if (Keyboard.current != null && Keyboard.current[Key.G].wasPressedThisFrame)
        {
            RequestDropCurrentItem();
        }
    }

    // --- [기능 1: 조준 및 HUD/외곽선 제어] ---
    // PlayerInventory.cs 내부 수정/추가 부분

    private void CheckInteraction()
    {
        // 1. 카메라 체크
        if (Camera.main == null) return;

        // 2. 레이 생성 및 시각화
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Debug.DrawRay(ray.origin, ray.direction * interactRange, Color.red);

        // 3. 무차별 레이캐스트 (마지막 인자인 LayerMask를 일부러 뺐습니다)
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // [여기에 로그가 찍히는지 확인하세요!]
            Debug.Log($"<color=cyan>무언가 부딪힘!</color> 이름: {hit.collider.name} / 레이어: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

            // 아이템 레이어인지 체크
            if (((1 << hit.collider.gameObject.layer) & itemLayer) != 0)
            {
                if (hit.collider.TryGetComponent(out ItemBase targetItem))
                {
                    if (lastLookedItem != targetItem)
                    {
                        ClearHighlight();
                        lastLookedItem = targetItem;
                        if (lastLookedItem.TryGetComponent(out Outline outline)) outline.enabled = true;
                        // InteractionUI.Instance.Show(targetItem.itemData.itemName);
                    }
                    return;
                }
                else
                {
                    Debug.LogWarning($"{hit.collider.name}은 Item 레이어지만 ItemBase 스크립트가 없습니다!");
                }
            }
        }
        else
        {
            // 아무것도 안 맞았을 때 (너무 멀거나 콜라이더가 없거나)
            ClearHighlight();
        }
    }

    // 하이라이트 상태를 초기화하는 보조 함수
    private void ClearHighlight()
    {
        if (lastLookedItem != null)
        {
            // 외곽선 끄기
            if (lastLookedItem.TryGetComponent(out Outline outline))
            {
                outline.enabled = false;
            }

            // UI 숨기기
            // InteractionUI.Instance.Hide();

            lastLookedItem = null;
        }
    }

    private void ClearLastLookedItem()
    {
        if (lastLookedItem != null)
        {
            // if (lastLookedItem.TryGetComponent(out Outline outline)) outline.enabled = false;
            // InteractionUI.Instance.HideHUD();
            lastLookedItem = null;
        }
    }

    // --- [기능 2: 아이템 습득 (팀원 추가 규칙 반영)] ---
    private void TryPickUpAction()
    {
        if (lastLookedItem != null)
        {
            LocalPickUpLogic(lastLookedItem);
        }
    }

    private void LocalPickUpLogic(ItemBase targetItem)
    {

        // [추가] 줍는 순간 해당 아이템의 외곽선을 무조건 끕니다.
        if (targetItem.TryGetComponent(out Outline outline))
        {
            outline.enabled = false;
        }

        // [추가] 조준 중이던 정보를 비워주어, 습득 후에도 HUD가 남아있는 현상 방지
        if (lastLookedItem == targetItem)
        {
            lastLookedItem = null;
            // InteractionUI.Instance.Hide(); // HUD 쓰고 있다면 여기서도 호출
        }


        // 양손 아이템일 경우
        if (targetItem.itemData.handType == HandType.TwoHand)
        {
            if (twoHandedItem != null) return;

            if (slots[currentSlotIndex] != null) slots[currentSlotIndex].gameObject.SetActive(false);
            twoHandedItem = targetItem;
            targetItem.RequestChangeOwnership(true, bothHandsTransform);
            currentWeightPenalty = 0.7f;
            OnTwoHandedToggled?.Invoke(true);
        }
        // 한손 아이템일 경우 (규칙: 현재 슬롯 우선 -> 낮은 인덱스 빈칸 -> 꽉 찼으면 스왑)
        else
        {
            if (twoHandedItem != null) return;

            int targetSlot = -1;

            // 1. 현재 선택된 슬롯이 비었는지 확인
            if (slots[currentSlotIndex] == null)
            {
                targetSlot = currentSlotIndex;
            }
            // 2. 비어있지 않다면 0번부터 순차 탐색
            else
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) { targetSlot = i; break; }
                }
            }

            // 3. 전체가 꽉 찼다면 현재 슬롯 아이템 버리고 교체
            if (targetSlot == -1)
            {
                RequestDropCurrentItem();
                targetSlot = currentSlotIndex;
            }

            slots[targetSlot] = targetItem;
            targetItem.RequestChangeOwnership(true, leftHandTransform);

            // 현재 보고 있는 슬롯이 아니면 가방에 넣기(비활성화)
            if (targetSlot != currentSlotIndex) targetItem.gameObject.SetActive(false);
        }

        OnInventoryUpdated?.Invoke();
        ClearLastLookedItem(); // 습득 후 HUD 정리
    }

    // --- [기능 3: 아이템 버리기 (바닥 뚫림 방지)] ---
    public void RequestDropCurrentItem()
    {
        ItemBase itemToDrop = null;

        if (twoHandedItem != null)
        {
            itemToDrop = twoHandedItem;
            twoHandedItem = null;
            currentWeightPenalty = 1.0f;
            OnTwoHandedToggled?.Invoke(false);
        }
        else if (slots[currentSlotIndex] != null)
        {
            itemToDrop = slots[currentSlotIndex];
            slots[currentSlotIndex] = null;
        }

        if (itemToDrop != null)
        {
            // [추가] 버리기 전에 외곽선을 확실히 끕니다.
            if (itemToDrop.TryGetComponent(out Outline outline))
            {
                outline.enabled = false;
            }

            // [추가] 인벤토리 시스템이 해당 아이템을 조준 중이라고 착각하지 않게 초기화
            if (lastLookedItem == itemToDrop)
            {
                lastLookedItem = null;
            }

            itemToDrop.RequestChangeOwnership(false, null);

            // [수정] 플레이어 발밑이 아닌 앞쪽 위에서 생성하여 물리 충돌 안정성 확보
            Vector3 dropPos = transform.position + transform.forward * 1.2f + Vector3.up * 0.5f;
            itemToDrop.transform.position = dropPos;

            // [수정] Rigidbody 초기화 및 연속 충돌 모드 설정
            if (itemToDrop.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // 양손템 버렸을 때 기존 한손템 다시 꺼내기
            if (twoHandedItem == null && slots[currentSlotIndex] != null)
                slots[currentSlotIndex].gameObject.SetActive(true);

            OnInventoryUpdated?.Invoke();
        }
    }

    // --- [기능 4: 슬롯 변경 (범위 고정형 Clamp)] ---
    private void HandleSlotChange()
    {
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        if (scroll != 0f)
        {
            int prevIndex = currentSlotIndex;

            // [수정] Mathf.Clamp 대신 직관적인 조건문 사용
            if (scroll < 0f) // 휠 올림 -> 다음 슬롯
            {
                if (currentSlotIndex < slots.Length - 1) currentSlotIndex++;
            }
            else if (scroll > 0f) // 휠 내림 -> 이전 슬롯
            {
                if (currentSlotIndex > 0) currentSlotIndex--;
            }

            // 인덱스가 실제로 바뀌었을 때만 3D 모델 및 UI 갱신
            if (prevIndex != currentSlotIndex)
            {
                // (아이템 활성화/비활성화 및 이벤트 호출 로직은 동일)
                if (twoHandedItem == null && slots[prevIndex] != null)
                    slots[prevIndex].gameObject.SetActive(false);

                if (twoHandedItem == null && slots[currentSlotIndex] != null)
                    slots[currentSlotIndex].gameObject.SetActive(true);

                OnSlotChanged?.Invoke(currentSlotIndex);
            }
        }
    }

    private int GetEmptySlotIndex() // (내부 사용용)
    {
        for (int i = 0; i < slots.Length; i++) if (slots[i] == null) return i;
        return -1;
    }
}