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
        else RequestTransitionServerRpc(player.OwnerClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTransitionServerRpc(ulong clientId)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client))
        {
            var p = client.PlayerObject.GetComponent<PlayerInventory>();
            if (p != null) PerformTransitionLogic(p);
        }
    }

    private void PerformTransitionLogic(PlayerInventory player)
    {
        GameSessionManager.Instance.truckItems.Clear();
        GameSessionManager.Instance.playerItems.Clear();

        BoxCollider zoneCol = GetComponent<BoxCollider>();
        Vector3 center = transform.position + transform.TransformDirection(zoneCol.center);
        Vector3 halfExtents = Vector3.Scale(zoneCol.size, transform.lossyScale) * 0.5f;

        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);
        int totalValue = 0;

        // 1. 바닥 짐 정산
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

        // 2. 가방 정산 (💡 여기서 양손템도 가방 1칸을 차지하므로 자동으로 같이 정산됨!)
        for (int i = 0; i < player.slots.Length; i++)
        {
            if (player.slots[i] != null)
            {
                if (player.slots[i] is Item_Scrap scrapItem)
                {
                    totalValue += scrapItem.currentScrapValue;
                    Debug.Log($"<color=yellow>[판매]</color> 가방 속 {scrapItem.itemData.itemName} (+{scrapItem.currentScrapValue})");
                }
                else
                {
                    SaveToPlayer(player.slots[i], i, player.OwnerClientId);
                }

                if (player.slots[i].NetworkObject != null && player.slots[i].NetworkObject.IsSpawned)
                    player.slots[i].NetworkObject.Despawn();
                player.slots[i] = null;
            }
        }

        // 3. 💡 [수정] 아이템은 위에서 정산+삭제 되었으므로, 양손 UI 상태만 초기화
        if (player.twoHandedItem != null)
        {
            player.twoHandedItem = null;
            player.OnTwoHandedToggled?.Invoke(false);
        }

        // 4. 외부 방치 아이템 강제 청소
        ItemBase[] allItemsInScene = FindObjectsByType<ItemBase>(FindObjectsSortMode.None);
        foreach (var leftoverItem in allItemsInScene)
        {
            if (leftoverItem != null && leftoverItem.NetworkObject != null && leftoverItem.NetworkObject.IsSpawned)
            {
                leftoverItem.NetworkObject.Despawn();
            }
        }

        GameSessionManager.Instance.AddMoney(totalValue);
        Debug.Log($"<color=cyan><b>[Ship System]</b> {nextSceneName}으로 이동합니다.</color>");
        StartCoroutine(WaitAndLoadSceneSafe());
    }

    private IEnumerator WaitAndLoadSceneSafe()
    {
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
        yield return new WaitForSecondsRealtime(0.2f);
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

            // 💡 [수정] 가방 먼저 싹 검사
            for (int i = 0; i < player.slots.Length; i++)
            {
                if (player.slots[i] != null && player.slots[i] is Item_Scrap slotScrap)
                {
                    totalValue += slotScrap.currentScrapValue;
                    if (slotScrap.NetworkObject != null && slotScrap.NetworkObject.IsSpawned) slotScrap.NetworkObject.Despawn();

                    // 만약 방금 판 폐지가 양손 템이었다면 참조 날리기
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

        if (totalValue > 0) GameSessionManager.Instance.AddMoney(totalValue);
    }

    private void SaveToTruck(ItemBase item)
    {
        GameSessionManager.Instance.truckItems.Add(new ItemSaveData
        {
            itemID = item.itemData.itemID,
            localPos = anchor.InverseTransformPoint(item.transform.position),
            localRot = Quaternion.Inverse(anchor.rotation) * item.transform.rotation,
            stateValues = new float[] { (item is Item_Durability dur) ? dur.currentDurability : 0 },
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
            stateValues = new float[] { (item is Item_Durability dur) ? dur.currentDurability : 0 }
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
            if (spawned is Item_Durability dur) dur.currentDurability = d.stateValues[0];
            spawned.GetComponent<NetworkObject>().Spawn();
        }
    }
}