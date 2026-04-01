using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider))]
public class SettlementZone : MonoBehaviour
{
    public Transform anchor;
    public string nextSceneName;

    private void Start() { SpawnItems(); }

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
            if (item != null && !item.isEquipped)
            {
                if (item is Item_Scrap scrap)
                {
                    totalValue += scrap.currentScrapValue;
                    Debug.Log($"<color=yellow>[판매]</color> {item.itemData.itemName} (+{scrap.currentScrapValue})");
                }
                else
                {
                    SaveToTruck(item);
                    Debug.Log($"<color=green>[보존]</color> {item.itemData.itemName} 위치 저장");
                }
                item.RequestDespawn();
            }
        }

        // 플레이어 인벤토리 저장
        for (int i = 0; i < player.slots.Length; i++)
        {
            if (player.slots[i] != null) SaveToPlayer(player.slots[i], i);
        }

        GameSessionManager.Instance.AddMoney(totalValue);
        Debug.Log($"<color=cyan><b>[Ship System]</b> {nextSceneName}으로 이동합니다.</color>");
        SceneManager.LoadScene(nextSceneName);
    }

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
        // 1. 데이터가 있는지 확인
        int dataCount = GameSessionManager.Instance.truckItems.Count;
        Debug.Log($"<color=white><b>[Ship System]</b> 복구할 아이템 데이터 개수: {dataCount}개</color>");

        if (dataCount <= 0) return;

        foreach (var d in GameSessionManager.Instance.truckItems)
        {
            // 2. 프리팹 찾기 시도
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(d.itemID);

            if (prefab == null)
            {
                Debug.LogError($"🚨 <b>[Spawn Error]</b> ID {d.itemID}번에 해당하는 프리팹을 DB에서 찾을 수 없습니다! (등록 확인 필요)");
                continue;
            }

            // 3. Anchor 확인
            if (anchor == null)
            {
                Debug.LogError("🚨 <b>[Spawn Error]</b> SettlementZone에 Anchor(트럭 바닥)가 연결되지 않았습니다!");
                return;
            }

            // 4. 소환 실행
            Vector3 spawnPos = anchor.TransformPoint(d.localPos);
            Quaternion spawnRot = anchor.rotation * d.localRot;
            ItemBase spawned = Instantiate(prefab, spawnPos, spawnRot);

            if (spawned is Item_Durability dur)
            {
                dur.currentDurability = d.stateValues[0];
            }

            Debug.Log($"<color=green><b>[Spawn Success]</b></color> {prefab.itemData.itemName} 소환 완료 (위치: {spawnPos})");
        }
    }
}