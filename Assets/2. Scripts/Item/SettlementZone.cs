using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;




[RequireComponent(typeof(BoxCollider))]
public class SettlementZone : NetworkBehaviour
{

    public Transform anchor;

    public Transform deliveryDropPoint; // 트럭 외부 (상점템+퀘스트템 스폰 위치)
    public float dropRadius = 2.0f;     // 아이템이 흩뿌려질 반경

    private void Start() => SpawnItems();

    public void ExecuteTransition(PlayerInventory player, string targetScene, bool doSettlement)
    {
        if (!IsSpawned) return;
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

        int totalScrapValue = 0;
        int recoveredPhonesCount = 0;
        List<PlayerInventory> playersInTruck = new List<PlayerInventory>();

        foreach (var t in targets)
        {
            ItemBase item = t.GetComponentInParent<ItemBase>();
            if (item != null && !item.isEquipped)
            {
                if (doSettlement)
                {
                    if (item.itemData.category == ItemCategory.Scrap)
                    {
                        totalScrapValue += (item is Item_Scrap scrap) ? scrap.currentScrapValue : item.itemData.basePrice;
                    }
                    else if (item.itemData.category == ItemCategory.Quest)
                    {
                        QuestManager.Instance.NotifyItemCollected(item.itemData.itemID, NetworkManager.ServerClientId);
                    }
                    else if (item.itemData.category == ItemCategory.Phone)
                    {
                        recoveredPhonesCount++;
                    }
                    else
                    {
                        SaveToTruck(item);
                    }
                }
                else { SaveToTruck(item); }

                if (doSettlement && (item.itemData.category == ItemCategory.Scrap || item.itemData.category == ItemCategory.Quest || item.itemData.category == ItemCategory.Phone))
                {
                    if (item.NetworkObject != null && item.NetworkObject.IsSpawned) item.NetworkObject.Despawn();
                }
            }

            PlayerInventory p = t.GetComponentInParent<PlayerInventory>();
            if (p != null && !playersInTruck.Contains(p)) playersInTruck.Add(p);
        }

        foreach (var p in playersInTruck)
        {
            for (int i = 0; i < p.slots.Length; i++)
            {
                ItemBase slotItem = p.slots[i];
                if (slotItem != null)
                {
                    if (doSettlement)
                    {
                        if (slotItem.itemData.category == ItemCategory.Scrap)
                        {
                            totalScrapValue += (slotItem is Item_Scrap s) ? s.currentScrapValue : slotItem.itemData.basePrice;
                        }
                        else if (slotItem.itemData.category == ItemCategory.Quest)
                        {
                            QuestManager.Instance.NotifyItemCollected(slotItem.itemData.itemID, p.OwnerClientId);
                        }
                        else if (slotItem.itemData.category == ItemCategory.Phone)
                        {
                            recoveredPhonesCount++;
                        }
                        else { SaveToPlayer(slotItem, i, p.OwnerClientId); }
                    }
                    else { SaveToPlayer(slotItem, i, p.OwnerClientId); }

                    if (doSettlement && (slotItem.itemData.category == ItemCategory.Scrap || slotItem.itemData.category == ItemCategory.Quest || slotItem.itemData.category == ItemCategory.Phone))
                    {
                        if (slotItem.NetworkObject != null && slotItem.NetworkObject.IsSpawned) slotItem.NetworkObject.Despawn();
                    }
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
            if (leftoverItem != null && leftoverItem.NetworkObject != null && leftoverItem.NetworkObject.IsSpawned) leftoverItem.NetworkObject.Despawn();
        }

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif

        if (doSettlement)
        {
            GameSessionManager.Instance.ProcessFinalSettlement(totalScrapValue, recoveredPhonesCount);
        }
        else
        {
            NetworkManager.Singleton.SceneManager.LoadScene(targetScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void SaveToTruck(ItemBase item)
    {
        GameSessionManager.Instance.truckItems.Add(new ItemSaveData { itemID = item.itemData.itemID, localPos = anchor.InverseTransformPoint(item.transform.position), localRot = Quaternion.Inverse(anchor.rotation) * item.transform.rotation, stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0, slotIndex = -1 });
    }

    private void SaveToPlayer(ItemBase item, int index, ulong pId)
    {
        if (!GameSessionManager.Instance.playerItems.ContainsKey(pId)) GameSessionManager.Instance.playerItems[pId] = new List<ItemSaveData>();
        GameSessionManager.Instance.playerItems[pId].Add(new ItemSaveData { itemID = item.itemData.itemID, slotIndex = index, stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0 });
    }

    private void SpawnItems()
    {
        if (!IsServer || GameSessionManager.Instance == null) return;

        // 1. 기존 트럭에 보존해둔 장비들 스폰 (트럭 내부 anchor 기준)
        foreach (var d in GameSessionManager.Instance.truckItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(d.itemID);
            if (prefab == null || anchor == null) continue;
            ItemBase spawned = Instantiate(prefab, anchor.TransformPoint(d.localPos), anchor.rotation * d.localRot);
            if (spawned is Item_Durability dur) dur.currentDurability = d.stateValue1;
            spawned.GetComponent<NetworkObject>().Spawn();
        }

        // 💡 2. 상점 구매템 + 퀘스트 지급템 스폰 (트럭 외부 deliveryDropPoint 기준)
        if (deliveryDropPoint != null)
        {
            foreach (int itemID in GameSessionManager.Instance.pendingSpawnItemIDs)
            {
                ItemBase prefab = GameSessionManager.Instance.GetPrefab(itemID);
                if (prefab != null)
                {
                    // dropRadius 반경 내에서 겹치지 않게 랜덤하게 흩뿌림
                    Vector2 randomCircle = Random.insideUnitCircle * dropRadius;
                    Vector3 randomOffset = new Vector3(randomCircle.x, 0.5f, randomCircle.y);

                    ItemBase spawned = Instantiate(prefab, deliveryDropPoint.position + randomOffset, deliveryDropPoint.rotation);
                    spawned.GetComponent<NetworkObject>().Spawn();
                }
            }
        }
        else
        {
            Debug.LogWarning("[SettlementZone] 택배를 내릴 deliveryDropPoint가 지정되지 않았습니다!");
        }

        // 스폰 완료 후 대기열 비우기
        GameSessionManager.Instance.pendingSpawnItemIDs.Clear();
    }

    //  유니티 에디터에서 배달 구역을 눈으로 볼 수 있게 해주는 기능
    private void OnDrawGizmos()
    {
        if (deliveryDropPoint != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f); // 반투명한 초록색
            Gizmos.DrawSphere(deliveryDropPoint.position, dropRadius);
        }
    }
}