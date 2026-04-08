using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(BoxCollider))]
public class SettlementZone : NetworkBehaviour
{
    public Transform anchor;

    private void Start() => SpawnItems();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[Key.F12].wasPressedThisFrame)
            if (IsServer) ProcessSettlement(); // F12 정산 테스트용
    }

    public void ExecuteTransition(PlayerInventory player, string targetScene, bool doSettlement)
    {
        if (!IsSpawned) return;
        Debug.Log($"<color=cyan><b>[Ship System]</b> 씬 이동 시작... (목적지: {targetScene} / 정산: {doSettlement})</color>");

        if (IsServer) PerformTransitionLogic(player, targetScene, doSettlement);
        else RequestTransitionServerRpc(targetScene, doSettlement);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTransitionServerRpc(string targetScene, bool doSettlement, RpcParams rpcParams = default)
    {
        ulong realSenderId = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(realSenderId, out var client))
        {
            var p = client.PlayerObject.GetComponent<PlayerInventory>();
            if (p != null) PerformTransitionLogic(p, targetScene, doSettlement);
        }
    }

    private void PerformTransitionLogic(PlayerInventory callerPlayer, string targetScene, bool doSettlement)
    {
        GameSessionManager.Instance.truckItems.Clear();
        GameSessionManager.Instance.playerItems.Clear();

        BoxCollider zoneCol = GetComponent<BoxCollider>();
        Vector3 center = transform.position + transform.TransformDirection(zoneCol.center);
        Vector3 halfExtents = Vector3.Scale(zoneCol.size, transform.lossyScale) * 0.5f;

        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);
        int totalValue = 0;

        // 💡 [핵심] 트럭(콜라이더) 안에 탑승한 플레이어만 담을 전용 리스트
        List<PlayerInventory> playersInTruck = new List<PlayerInventory>();

        // 1. 바닥 아이템 및 탑승자 스캔
        foreach (var t in targets)
        {
            // 1-1. 바닥 아이템 검사
            ItemBase item = t.GetComponentInParent<ItemBase>();
            if (item != null && !item.isEquipped)
            {
                if (doSettlement && item.itemData.category == ItemCategory.Scrap)
                {
                    int val = (item is Item_Scrap scrap) ? scrap.currentScrapValue : item.itemData.basePrice;
                    totalValue += val;
                    Debug.Log($"<color=yellow>[판매]</color> 바닥의 {item.itemData.itemName} (+{val})");
                }
                else
                {
                    SaveToTruck(item);
                    Debug.Log($"<color=green>[보존]</color> {item.itemData.itemName} 위치 저장 완료");
                }
                if (item.NetworkObject != null && item.NetworkObject.IsSpawned) item.NetworkObject.Despawn();
            }

            // 1-2. 콜라이더 안에 플레이어가 있으면 탑승자 명단에 추가
            PlayerInventory p = t.GetComponentInParent<PlayerInventory>();
            if (p != null && !playersInTruck.Contains(p))
            {
                playersInTruck.Add(p);
            }
        }

        // 2. 맵 전체가 아니라 '탑승자 명단(playersInTruck)'에 있는 사람의 가방만 검사
        foreach (var p in playersInTruck)
        {
            for (int i = 0; i < p.slots.Length; i++)
            {
                if (p.slots[i] != null)
                {
                    if (doSettlement && p.slots[i].itemData.category == ItemCategory.Scrap)
                    {
                        int val = (p.slots[i] is Item_Scrap scrapItem) ? scrapItem.currentScrapValue : p.slots[i].itemData.basePrice;
                        totalValue += val;
                        Debug.Log($"<color=yellow>[판매]</color> {p.OwnerClientId}번(탑승자) 가방 속 {p.slots[i].itemData.itemName} (+{val})");
                    }
                    else
                    {
                        SaveToPlayer(p.slots[i], i, p.OwnerClientId);
                    }

                    if (p.slots[i].NetworkObject != null && p.slots[i].NetworkObject.IsSpawned)
                        p.slots[i].NetworkObject.Despawn();
                    p.slots[i] = null;
                }
            }

            if (p.twoHandedItem != null)
            {
                p.twoHandedItem = null;
                p.OnTwoHandedToggled?.Invoke(false);
            }
        }

        // 3. 맵에 남은 아이템 찌꺼기 완벽 삭제 (고스트 버그 방지)
        ItemBase[] allItemsInScene = FindObjectsByType<ItemBase>(FindObjectsSortMode.None);
        foreach (var leftoverItem in allItemsInScene)
        {
            if (leftoverItem != null && leftoverItem.NetworkObject != null && leftoverItem.NetworkObject.IsSpawned)
            {
                leftoverItem.NetworkObject.Despawn();
            }
        }

        // 4. 정산 결과 방송 및 씬 이동
        if (doSettlement && totalValue > 0) BroadcastSettlementResultClientRpc(totalValue);

        Debug.Log($"<color=cyan><b>[Ship System]</b> {targetScene} 씬으로 이동합니다. (탑승 인원: {playersInTruck.Count}명)</color>");
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
        NetworkManager.Singleton.SceneManager.LoadScene(targetScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void ProcessSettlement()
    {
        int totalValue = 0;
        BoxCollider zoneCol = GetComponent<BoxCollider>();
        Vector3 center = transform.position + transform.TransformDirection(zoneCol.center);
        Vector3 halfExtents = Vector3.Scale(zoneCol.size, transform.lossyScale) * 0.5f;
        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);

        List<PlayerInventory> playersInTruck = new List<PlayerInventory>();

        foreach (var t in targets)
        {
            ItemBase item = t.GetComponentInParent<ItemBase>();
            if (item != null && !item.isEquipped && item is Item_Scrap scrap)
            {
                totalValue += scrap.currentScrapValue;
                if (scrap.NetworkObject != null && scrap.NetworkObject.IsSpawned) scrap.NetworkObject.Despawn();
            }

            PlayerInventory p = t.GetComponentInParent<PlayerInventory>();
            if (p != null && !playersInTruck.Contains(p))
            {
                playersInTruck.Add(p);
            }
        }

        foreach (var player in playersInTruck)
        {
            bool inventoryChanged = false;

            for (int i = 0; i < player.slots.Length; i++)
            {
                if (player.slots[i] != null && player.slots[i] is Item_Scrap slotScrap)
                {
                    totalValue += slotScrap.currentScrapValue;
                    if (slotScrap.NetworkObject != null && slotScrap.NetworkObject.IsSpawned) slotScrap.NetworkObject.Despawn();

                    if (player.twoHandedItem == player.slots[i])
                    {
                        player.twoHandedItem = null;
                        player.OnTwoHandedToggled?.Invoke(false);
                    }

                    player.slots[i] = null;
                    inventoryChanged = true;
                }
            }

            if (inventoryChanged) player.OnInventoryUpdated?.Invoke();
        }

        if (totalValue > 0) BroadcastSettlementResultClientRpc(totalValue);
    }

    [Rpc(SendTo.Everyone)]
    private void BroadcastSettlementResultClientRpc(int addedMoney)
    {
        if (addedMoney > 0)
        {
            GameSessionManager.Instance.AddMoney(addedMoney);
        }
    }

    private void SaveToTruck(ItemBase item)
    {
        GameSessionManager.Instance.truckItems.Add(new ItemSaveData
        {
            itemID = item.itemData.itemID,
            localPos = anchor.InverseTransformPoint(item.transform.position),
            localRot = Quaternion.Inverse(anchor.rotation) * item.transform.rotation,
            stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0,
            slotIndex = -1
        });
    }

    private void SaveToPlayer(ItemBase item, int index, ulong pId)
    {
        if (!GameSessionManager.Instance.playerItems.ContainsKey(pId))
            GameSessionManager.Instance.playerItems[pId] = new List<ItemSaveData>();

        GameSessionManager.Instance.playerItems[pId].Add(new ItemSaveData
        {
            itemID = item.itemData.itemID,
            slotIndex = index,
            stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0
        });
    }

    private void SpawnItems()
    {
        if (!IsServer || GameSessionManager.Instance == null) return;
        foreach (var d in GameSessionManager.Instance.truckItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(d.itemID);
            if (prefab == null || anchor == null) continue;
            ItemBase spawned = Instantiate(prefab, anchor.TransformPoint(d.localPos), anchor.rotation * d.localRot);
            if (spawned is Item_Durability dur) dur.currentDurability = d.stateValue1;
            spawned.GetComponent<NetworkObject>().Spawn();
        }
    }
}