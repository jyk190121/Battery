using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

[RequireComponent(typeof(BoxCollider))]
public class SettlementZone : NetworkBehaviour
{
    private bool isTransitioning = false;
    public Transform anchor;
    public Transform deliveryDropPoint;
    public float dropRadius = 2.0f;

    private BoxCollider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
    }

    private void Start()
    {
        SpawnItems();

        if (IsServer)
        {
            StartCoroutine(TruckScanRoutine());
        }
    }

    private IEnumerator TruckScanRoutine()
    {
        WaitForSeconds waitDelay = new WaitForSeconds(0.5f);

        while (true)
        {
            yield return waitDelay;

            if (isTransitioning || QuestManager.Instance == null)
            {
                continue;
            }

            Vector3 center = transform.position + transform.TransformDirection(zoneCollider.center);
            Vector3 halfExtents = Vector3.Scale(zoneCollider.size, transform.lossyScale) * 0.5f;
            Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);

            HashSet<ItemBase> uniqueItems = new HashSet<ItemBase>();
            List<int> currentDetectedIds = new List<int>();

            foreach (Collider targetCollider in targets)
            {
                ItemBase item = targetCollider.GetComponentInParent<ItemBase>();

                if (item != null && item.itemData != null && !item.isEquipped && uniqueItems.Add(item))
                {
                    currentDetectedIds.Add(item.itemData.itemID);
                }
            }

            foreach (int detectedId in currentDetectedIds)
            {
                if (!QuestManager.Instance.itemsInTruck.Contains(detectedId))
                {
                    QuestManager.Instance.itemsInTruck.Add(detectedId);
                }
            }

            for (int index = QuestManager.Instance.itemsInTruck.Count - 1; index >= 0; index--)
            {
                int trackedId = QuestManager.Instance.itemsInTruck[index];
                if (!currentDetectedIds.Contains(trackedId))
                {
                    QuestManager.Instance.itemsInTruck.RemoveAt(index);
                }
            }
        }
    }

    public void ExecuteTransition(PlayerInventory player, string targetScene, bool doSettlement)
    {
        if (!IsSpawned || isTransitioning)
        {
            return;
        }

        string cleanedScene = targetScene.Trim();
        Debug.Log($"<color=cyan>[Ship System]</color> 이동 요청 접수 (목적지: {cleanedScene} / 정산여부: {doSettlement})");

        if (IsServer)
        {
            StartCoroutine(PerformTransitionSequence(player, cleanedScene, doSettlement));
        }
        else
        {
            RequestTransitionServerRpc(player.OwnerClientId, cleanedScene, doSettlement);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTransitionServerRpc(ulong callerId, string targetScene, bool doSettlement, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(callerId, out NetworkClient client))
        {
            PlayerInventory player = client.PlayerObject.GetComponent<PlayerInventory>();
            StartCoroutine(PerformTransitionSequence(player, targetScene, doSettlement));
        }
    }

    private IEnumerator PerformTransitionSequence(PlayerInventory caller, string targetScene, bool doSettlement)
    {
        if (!IsServer || isTransitioning)
        {
            yield break;
        }

        isTransitioning = true;

        if (GameSessionManager.Instance == null || QuestManager.Instance == null || GameMaster.Instance == null)
        {
            Debug.LogError("<color=red>[Error] Required Managers missing!</color>");
            isTransitioning = false;
            yield break;
        }

        GameSessionManager.Instance.truckItems.Clear();
        GameSessionManager.Instance.playerItems.Clear();

        Vector3 center = transform.position + transform.TransformDirection(zoneCollider.center);
        Vector3 halfExtents = Vector3.Scale(zoneCollider.size, transform.lossyScale) * 0.5f;
        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);

        int totalScrapValue = 0;
        int recoveredPhonesCount = 0;
        List<PlayerInventory> playersInTruck = new List<PlayerInventory>();
        List<ulong> survivorIds = new List<ulong>();
        List<ItemBase> processedFloorItems = new List<ItemBase>();

        if (caller != null && !survivorIds.Contains(caller.OwnerClientId))
        {
            playersInTruck.Add(caller);
            survivorIds.Add(caller.OwnerClientId);
        }

        foreach (Collider targetCollider in targets)
        {
            ItemBase item = targetCollider.GetComponentInParent<ItemBase>();

            if (item != null && !item.isEquipped && !processedFloorItems.Contains(item))
            {
                processedFloorItems.Add(item);

                if (doSettlement)
                {
                    if (item.itemData.category == ItemCategory.Scrap ||
                        item.itemData.category == ItemCategory.Phone ||
                        item.itemData.category == ItemCategory.Quest)
                    {
                        if (item.itemData.category == ItemCategory.Scrap)
                        {
                            totalScrapValue += (item is Item_Scrap scrap) ? scrap.currentScrapValue : item.itemData.basePrice;
                        }

                        if (item.itemData.category == ItemCategory.Phone)
                        {
                            recoveredPhonesCount++;
                        }

                        QuestManager.Instance.NotifyFinalClear(item.itemData.itemID, NetworkManager.ServerClientId);
                    }
                    else
                    {
                        SaveToTruck(item);
                    }
                }
                else
                {
                    SaveToTruck(item);
                }
            }

            PlayerInventory playerInventory = targetCollider.GetComponentInParent<PlayerInventory>();
            if (playerInventory != null && !playersInTruck.Contains(playerInventory))
            {
                playersInTruck.Add(playerInventory);
                survivorIds.Add(playerInventory.OwnerClientId);
            }
        }

        foreach (PlayerInventory player in playersInTruck)
        {
            for (int slotIndex = 0; slotIndex < player.slots.Length; slotIndex++)
            {
                ItemBase slotItem = player.slots[slotIndex];
                if (slotItem != null)
                {
                    if (doSettlement)
                    {
                        if (slotItem.itemData.category == ItemCategory.Scrap)
                        {
                            totalScrapValue += (slotItem is Item_Scrap scrapItem) ? scrapItem.currentScrapValue : slotItem.itemData.basePrice;
                        }

                        if (slotItem.itemData.category == ItemCategory.Phone)
                        {
                            recoveredPhonesCount++;
                        }

                        QuestManager.Instance.NotifyFinalClear(slotItem.itemData.itemID, player.OwnerClientId);

                        if (slotItem.itemData.category == ItemCategory.Scrap ||
                            slotItem.itemData.category == ItemCategory.Quest ||
                            slotItem.itemData.category == ItemCategory.Phone)
                        {
                            player.RemoveItemByServer(slotItem.itemData.itemID);
                            continue;
                        }
                    }

                    SaveToPlayer(slotItem, slotIndex, player.OwnerClientId);
                    player.slots[slotIndex] = null;
                }
            }

            if (player.twoHandedItem != null)
            {
                ItemBase twoHandedItem = player.twoHandedItem;

                if (doSettlement)
                {
                    if (twoHandedItem.itemData.category == ItemCategory.Scrap)
                    {
                        totalScrapValue += (twoHandedItem is Item_Scrap scrapItem) ? scrapItem.currentScrapValue : twoHandedItem.itemData.basePrice;
                    }

                    if (twoHandedItem.itemData.category == ItemCategory.Phone)
                    {
                        recoveredPhonesCount++;
                    }

                    QuestManager.Instance.NotifyFinalClear(twoHandedItem.itemData.itemID, player.OwnerClientId);

                    if (twoHandedItem.itemData.category == ItemCategory.Scrap ||
                        twoHandedItem.itemData.category == ItemCategory.Quest ||
                        twoHandedItem.itemData.category == ItemCategory.Phone)
                    {
                        player.RemoveItemByServer(twoHandedItem.itemData.itemID);
                        continue;
                    }
                }

                SaveToPlayer(twoHandedItem, -1, player.OwnerClientId);
                player.twoHandedItem = null;
                player.OnTwoHandedToggled?.Invoke(false);
            }
        }

        if (doSettlement)
        {
            foreach (int itemId in QuestManager.Instance.itemsInTruck)
            {
                QuestManager.Instance.NotifyFinalClear(itemId, NetworkManager.ServerClientId);
            }
        }

        GameSessionManager.Instance.CleanupAllItemsInScene();

        if (doSettlement && QuestCameraBridge.Instance != null)
        {
            QuestCameraBridge.Instance.CommandSubmitDataClientRpc(survivorIds.ToArray());
            yield return new WaitForSeconds(1.0f);
        }

        if (doSettlement)
        {
            try
            {
                (int questIncome, int questScore) = QuestManager.Instance.GetCalculatedQuestResults();

                int totalQuests = QuestManager.Instance.activeQuests.Count;
                int clearedQuests = QuestManager.Instance.serverCompletedQuests.Count;

                Debug.Log($"<color=cyan><b>[최종 결산]</b></color> 퀘스트: {clearedQuests}/{totalQuests} " +
                          $"(자금: {questIncome} / 실적: {questScore}pt)");

                int finalDailyIncome = totalScrapValue + questIncome;
                int deadCount = GameSessionManager.Instance.deadPlayersCount;
                int missingPhones = Mathf.Max(0, deadCount - recoveredPhonesCount);
                float penaltyMultiplier = 1.0f - (missingPhones * 0.05f);
                int finalNetIncome = Mathf.RoundToInt(finalDailyIncome * penaltyMultiplier);

                bool isWipedOut = deadCount >= GameSessionManager.Instance.GetTotalPlayers();

                GameMaster.Instance.SetPendingResults(isWipedOut, finalNetIncome, questScore);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Settlement] Error: {e.Message}");
            }
        }

        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(targetScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            isTransitioning = false;
        }
    }

    private void SaveToTruck(ItemBase item)
    {
        GameSessionManager.Instance.truckItems.Add(new ItemSaveData
        {
            itemID = item.itemData.itemID,
            localPos = anchor.InverseTransformPoint(item.transform.position),
            localRot = Quaternion.Inverse(anchor.rotation) * item.transform.rotation,
            stateValue1 = (item is Item_Durability durabilityItem) ? durabilityItem.currentDurability : 0,
            slotIndex = -1
        });
    }

    private void SaveToPlayer(ItemBase item, int index, ulong playerId)
    {
        if (!GameSessionManager.Instance.playerItems.ContainsKey(playerId))
        {
            GameSessionManager.Instance.playerItems[playerId] = new List<ItemSaveData>();
        }

        GameSessionManager.Instance.playerItems[playerId].Add(new ItemSaveData
        {
            itemID = item.itemData.itemID,
            slotIndex = index,
            stateValue1 = (item is Item_Durability durabilityItem) ? durabilityItem.currentDurability : 0
        });
    }

    private void SpawnItems()
    {
        if (!IsServer || GameSessionManager.Instance == null)
        {
            return;
        }

        Debug.Log($"<color=lime>[SettlementZone]</color> 아이템 복구 시작. 남은 짐 개수: {GameSessionManager.Instance.truckItems.Count}");

        foreach (ItemSaveData saveData in GameSessionManager.Instance.truckItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(saveData.itemID);
            if (prefab == null || anchor == null)
            {
                continue;
            }

            ItemBase spawned = Instantiate(prefab, anchor.TransformPoint(saveData.localPos), anchor.rotation * saveData.localRot);

            if (spawned is Item_Durability durabilityItem)
            {
                durabilityItem.currentDurability = saveData.stateValue1;
            }

            spawned.GetComponent<NetworkObject>().Spawn();
        }

        GameSessionManager.Instance.truckItems.Clear();

        if (deliveryDropPoint != null)
        {
            foreach (int itemID in GameSessionManager.Instance.pendingSpawnItemIDs)
            {
                ItemBase prefab = GameSessionManager.Instance.GetPrefab(itemID);
                if (prefab != null)
                {
                    Vector2 randomCircle = Random.insideUnitCircle * dropRadius;
                    Vector3 randomOffset = new Vector3(randomCircle.x, 0.5f, randomCircle.y);

                    ItemBase spawned = Instantiate(prefab, deliveryDropPoint.position + randomOffset, deliveryDropPoint.rotation);
                    spawned.GetComponent<NetworkObject>().Spawn();
                }
            }
            GameSessionManager.Instance.pendingSpawnItemIDs.Clear();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestReviveAllPlayersServerRpc()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (PlayerController playerController in PlayerController.AllPlayers)
        {
            playerController.RevivePlayer();
        }
    }

    private void OnDrawGizmos()
    {
        if (deliveryDropPoint != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawSphere(deliveryDropPoint.position, dropRadius);
        }
    }
}