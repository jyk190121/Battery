using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SettlementZone : MonoBehaviour
{
    [Header("Detected Scraps")]
    public List<Item_Scrap> scrapsInZone = new List<Item_Scrap>();

    // 구역 안에 들어온 플레이어를 기억할 변수
    private PlayerInventory playerInZone;

    private void Update()
    {
        // [테스트용] F12 키를 누르면 즉시 정산 실행
        if (Keyboard.current != null && Keyboard.current[Key.F12].wasPressedThisFrame)
        {
            ProcessSettlement();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 플레이어가 들어왔는지 확인
        if (other.TryGetComponent(out PlayerInventory player))
        {
            playerInZone = player;
            Debug.Log("플레이어 정산 구역 진입. (가방 안의 폐지도 정산 가능)");
        }

        // 2. 바닥에 떨어진 폐지가 들어왔는지 확인
        Item_Scrap scrap = other.GetComponentInParent<Item_Scrap>();
        if (scrap != null)
        {
            if (!scrapsInZone.Contains(scrap))
            {
                scrapsInZone.Add(scrap);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 1. 플레이어가 나갔는지 확인
        if (other.TryGetComponent(out PlayerInventory player))
        {
            playerInZone = null;
        }

        // 2. 폐지가 밖으로 나갔는지 확인
        Item_Scrap scrap = other.GetComponentInParent<Item_Scrap>();
        if (scrap != null)
        {
            if (scrapsInZone.Contains(scrap))
            {
                scrapsInZone.Remove(scrap);
            }
        }
    }

    // 실제 정산 로직
    public void ProcessSettlement()
    {
        int totalValue = 0;

        // ========================================================
        // [1단계] 바닥에 떨어진 아이템들 정산
        // ========================================================
        for (int i = scrapsInZone.Count - 1; i >= 0; i--)
        {
            Item_Scrap scrap = scrapsInZone[i];

            if (scrap != null && !scrap.isEquipped) // 들고 있지 않은 것만 (안전장치)
            {
                totalValue += scrap.currentScrapValue;
                scrap.RequestDespawn(); // 파괴
                scrapsInZone.RemoveAt(i);
            }
        }

        // ========================================================
        // [2단계] 구역 안에 서 있는 플레이어의 인벤토리 검사
        // ========================================================
        if (playerInZone != null)
        {
            bool inventoryChanged = false;

            // 2-1. 양손에 들고 있는 아이템 확인
            if (playerInZone.twoHandedItem != null && playerInZone.twoHandedItem is Item_Scrap twoHandScrap)
            {
                totalValue += twoHandScrap.currentScrapValue;
                twoHandScrap.RequestDespawn();

                playerInZone.twoHandedItem = null;
                playerInZone.currentWeightPenalty = 1.0f;
                playerInZone.OnTwoHandedToggled?.Invoke(false);
                inventoryChanged = true;
            }

            // 2-2. 0~3번 슬롯(가방)에 있는 아이템들 확인
            for (int i = 0; i < playerInZone.slots.Length; i++)
            {
                if (playerInZone.slots[i] != null && playerInZone.slots[i] is Item_Scrap slotScrap)
                {
                    totalValue += slotScrap.currentScrapValue;
                    slotScrap.RequestDespawn();

                    playerInZone.slots[i] = null; // 가방에서 비우기
                    inventoryChanged = true;
                }
            }

            // 가방 안의 아이템이 팔렸다면 인벤토리 UI 즉시 갱신
            if (inventoryChanged)
            {
                playerInZone.OnInventoryUpdated?.Invoke();
            }
        }

        // ========================================================
        // [3단계] 최종 정산 처리
        // ========================================================
        if (totalValue > 0)
        {
            MoneyManager.Instance.AddMoney(totalValue);
        }
        else
        {
            Debug.Log("정산할 폐지가 없습니다. (일반 아이템은 팔리지 않습니다.)");
        }
    }
}