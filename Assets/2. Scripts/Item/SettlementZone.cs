using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(BoxCollider))]
public class SettlementZone : NetworkBehaviour
{
    public Transform anchor;
    public string nextSceneName;

    private void Start() => SpawnItems();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[Key.F12].wasPressedThisFrame)
            if (IsServer) ProcessSettlement();
    }

    public void ExecuteTransition(PlayerInventory player)
    {
        if (!IsSpawned) return;
        Debug.Log("<color=cyan><b>[Ship System]</b> 이륙 시퀀스 시작...</color>");

        if (IsServer) PerformTransitionLogic(player);
        else RequestTransitionServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTransitionServerRpc(RpcParams rpcParams = default)
    {
        ulong realSenderId = rpcParams.Receive.SenderClientId;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(realSenderId, out var client))
        {
            var p = client.PlayerObject.GetComponent<PlayerInventory>();
            if (p != null) PerformTransitionLogic(p);
        }
    }

    private void PerformTransitionLogic(PlayerInventory callerPlayer)
    {
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
                    Debug.Log($"<color=yellow>[판매]</color> 바닥의 {item.itemData.itemName} (+{scrap.currentScrapValue})");
                }
                else
                {
                    SaveToTruck(item);
                    Debug.Log($"<color=green>[보존]</color> {item.itemData.itemName} 위치 저장");
                }
                if (item.NetworkObject != null && item.NetworkObject.IsSpawned) item.NetworkObject.Despawn();
            }
        }

        PlayerInventory[] allPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            for (int i = 0; i < p.slots.Length; i++)
            {
                if (p.slots[i] != null)
                {
                    if (p.slots[i] is Item_Scrap scrapItem)
                    {
                        totalValue += scrapItem.currentScrapValue;
                        Debug.Log($"<color=yellow>[판매]</color> {p.OwnerClientId}번 가방 속 {scrapItem.itemData.itemName} (+{scrapItem.currentScrapValue})");
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

        ItemBase[] allItemsInScene = FindObjectsByType<ItemBase>(FindObjectsSortMode.None);
        foreach (var leftoverItem in allItemsInScene)
        {
            if (leftoverItem != null && leftoverItem.NetworkObject != null && leftoverItem.NetworkObject.IsSpawned)
            {
                leftoverItem.NetworkObject.Despawn();
            }
        }

        // 💡 [해결 1] 서버 혼자 계산하고 끝내지 않고, 모든 클라이언트에게 결과를 방송합니다!
        BroadcastSettlementResultClientRpc(totalValue);

        Debug.Log($"<color=cyan><b>[Ship System]</b> {nextSceneName}으로 이동합니다.</color>");

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
        NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void ProcessSettlement()
    {
        int totalValue = 0;
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
                if (scrap.NetworkObject != null && scrap.NetworkObject.IsSpawned) scrap.NetworkObject.Despawn();
            }
        }

        PlayerInventory[] allPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
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

        // 💡 F12 테스트 로직에도 방송 기능 추가
        if (totalValue > 0) BroadcastSettlementResultClientRpc(totalValue);
    }

    // 💡 [해결 1 핵심] 서버가 계산한 돈을 클라이언트들의 지갑에도 넣어주는 함수
    [Rpc(SendTo.Everyone)]
    private void BroadcastSettlementResultClientRpc(int addedMoney)
    {
        if (addedMoney > 0)
        {
            // 이 코드가 이제 클라이언트의 컴퓨터에서도 실행되므로, 
            // 클라이언트 콘솔창에도 똑같이 정산 로그(+XXX원)가 찍힙니다!
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
            stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0, // 구조체 패치 반영
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
            stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0 // 구조체 패치 반영
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
            if (spawned is Item_Durability dur) dur.currentDurability = d.stateValue1; // 구조체 패치 반영
            spawned.GetComponent<NetworkObject>().Spawn();
        }
    }
}