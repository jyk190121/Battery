using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class SettlementZone : MonoBehaviour
{
    public Transform anchor;
    public string nextSceneName;

    private void Start()
    {
        SpawnItems();
    }

    private void Update()
    {
        // F12 키를 누르면 즉시 정산 실행
        if (Keyboard.current != null && Keyboard.current[Key.F12].wasPressedThisFrame)
        {
            ProcessSettlement();
        }
    }

    // ==========================================================
    // 1. 씬 이동 및 데이터 보존 (이륙/출발 버튼용)
    // ==========================================================
    public void ExecuteTransition(PlayerInventory player)
    {
        Debug.Log("<color=cyan><b>[Ship System]</b> 이륙 시퀀스 시작...</color>");
        GameSessionManager.Instance.truckItems.Clear();
        GameSessionManager.Instance.playerItems.Clear();

        BoxCollider zoneCol = GetComponent<BoxCollider>();
        Vector3 center = transform.position + transform.TransformDirection(zoneCol.center);
        Vector3 halfExtents = Vector3.Scale(zoneCol.size, transform.lossyScale) * 0.5f;

        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);
        int totalValue = 0;

        foreach (var t in targets)
        {
            ItemBase item = t.GetComponentInParent<ItemBase>();
            // 바닥에 있는 아이템만 스캔 (!item.isEquipped)
            if (item != null && !item.isEquipped)
            {
                if (item is Item_Scrap scrap)
                {
                    totalValue += scrap.currentScrapValue;
                    Debug.Log($"<color=yellow>[판매]</color> 바닥의 {item.itemData.itemName} (+{scrap.currentScrapValue})");
                }
                else
                {
                    SaveToTruck(item);
                    Debug.Log($"<color=green>[보존]</color> {item.itemData.itemName} 위치 저장");
                }
                item.RequestDespawn();
            }
        }

        // 💡 [수정됨] 이륙할 때, 가방에 든 폐지도 팔고 돈으로 바꿈
        for (int i = 0; i < player.slots.Length; i++)
        {
            if (player.slots[i] != null)
            {
                if (player.slots[i] is Item_Scrap scrapItem)
                {
                    totalValue += scrapItem.currentScrapValue;
                    Debug.Log($"<color=yellow>[판매]</color> 가방 속 {scrapItem.itemData.itemName} (+{scrapItem.currentScrapValue})");
                    scrapItem.RequestDespawn();
                    player.slots[i] = null; // 가방 비우기
                }
                else
                {
                    SaveToPlayer(player.slots[i], i);
                }
            }
        }

        // 양손에 든 폐지도 판다
        if (player.twoHandedItem != null && player.twoHandedItem is Item_Scrap twoHandScrap)
        {
            totalValue += twoHandScrap.currentScrapValue;
            Debug.Log($"<color=yellow>[판매]</color> 양손에 든 {twoHandScrap.itemData.itemName} (+{twoHandScrap.currentScrapValue})");
            twoHandScrap.RequestDespawn();
            player.twoHandedItem = null;
            player.OnTwoHandedToggled?.Invoke(false);
        }

        GameSessionManager.Instance.AddMoney(totalValue);
        Debug.Log($"<color=cyan><b>[Ship System]</b> {nextSceneName}으로 이동합니다.</color>");
        SceneManager.LoadScene(nextSceneName);
    }

    // ==========================================================
    // 2. 구역 내 강제 정산 로직 (F12 디버깅 및 수동 정산용)
    // ==========================================================
    public void ProcessSettlement()
    {
        int totalValue = 0;
        bool inventoryChanged = false;

        Debug.Log("<color=yellow><b>[Settlement]</b> 물리 스캔 정산을 시작합니다...</color>");

        // 1. 바닥에 떨어진 아이템 스캔 (isEquipped가 false인 것만)
        BoxCollider zoneCol = GetComponent<BoxCollider>();
        Vector3 center = transform.position + transform.TransformDirection(zoneCol.center);
        Vector3 halfExtents = Vector3.Scale(zoneCol.size, transform.lossyScale) * 0.5f;
        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);

        foreach (var t in targets)
        {
            ItemBase item = t.GetComponentInParent<ItemBase>();
            if (item != null && !item.isEquipped && item is Item_Scrap scrap)
            {
                totalValue += scrap.currentScrapValue;
                Debug.Log($"바닥 폐지 정산: {item.itemData.itemName} (+{scrap.currentScrapValue}원)");
                scrap.RequestDespawn();
            }
        }

        // 2. 플레이어 인벤토리 직접 검사 (isEquipped 무시하고 강제로 검사)
        PlayerInventory player = FindFirstObjectByType<PlayerInventory>();

        if (player != null)
        {
            // 2-1. 양손에 들고 있는 폐지 확인
            if (player.twoHandedItem != null && player.twoHandedItem is Item_Scrap twoHandScrap)
            {
                totalValue += twoHandScrap.currentScrapValue;
                Debug.Log($"양손 폐지 정산: {twoHandScrap.itemData.itemName} (+{twoHandScrap.currentScrapValue}원)");
                twoHandScrap.RequestDespawn();

                player.twoHandedItem = null;
                player.OnTwoHandedToggled?.Invoke(false);
                inventoryChanged = true;
            }

            // 2-2. 가방(0~3번 슬롯)에 있는 폐지 강제 스캔
            for (int i = 0; i < player.slots.Length; i++)
            {
                // 💡 [핵심] 여기서 isEquipped 조건을 보지 않고, Item_Scrap인지 타입만 검사함!
                if (player.slots[i] != null && player.slots[i] is Item_Scrap slotScrap)
                {
                    totalValue += slotScrap.currentScrapValue;
                    Debug.Log($"가방 폐지 정산 [{i}번 슬롯]: {slotScrap.itemData.itemName} (+{slotScrap.currentScrapValue}원)");
                    slotScrap.RequestDespawn();

                    player.slots[i] = null;
                    inventoryChanged = true;
                }
            }

            // 가방 안의 아이템이 팔렸다면 인벤토리 UI 즉시 갱신
            if (inventoryChanged)
            {
                player.OnInventoryUpdated?.Invoke();
            }
        }

        // 3. 최종 금액 합산
        if (totalValue > 0)
        {
            GameSessionManager.Instance.AddMoney(totalValue);
        }
        else
        {
            Debug.Log("<color=grey>정산할 폐지가 없습니다.</color>");
        }
    }

    // ==========================================================
    // 3. 내부 저장 및 복구(스폰) 헬퍼 함수
    // ==========================================================
    private void SaveToTruck(ItemBase item)
    {
        ItemSaveData d = new ItemSaveData
        {
            itemID = item.itemData.itemID,
            localPos = anchor.InverseTransformPoint(item.transform.position),
            localRot = Quaternion.Inverse(anchor.rotation) * item.transform.rotation,
            stateValues = new float[] { (item is Item_Durability dur) ? dur.currentDurability : 0 },
            slotIndex = -1
        };
        GameSessionManager.Instance.truckItems.Add(d);
    }

    private void SaveToPlayer(ItemBase item, int index)
    {
        ItemSaveData d = new ItemSaveData
        {
            itemID = item.itemData.itemID,
            slotIndex = index,
            stateValues = new float[] { (item is Item_Durability dur) ? dur.currentDurability : 0 }
        };
        GameSessionManager.Instance.playerItems.Add(d);
    }

    private void SpawnItems()
    {
        int dataCount = GameSessionManager.Instance.truckItems.Count;
        if (dataCount <= 0) return;

        foreach (var d in GameSessionManager.Instance.truckItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(d.itemID);

            if (prefab == null || anchor == null) continue;

            Vector3 spawnPos = anchor.TransformPoint(d.localPos);
            Quaternion spawnRot = anchor.rotation * d.localRot;
            ItemBase spawned = Instantiate(prefab, spawnPos, spawnRot);

            if (spawned is Item_Durability dur)
            {
                dur.currentDurability = d.stateValues[0];
            }
        }
    }
}