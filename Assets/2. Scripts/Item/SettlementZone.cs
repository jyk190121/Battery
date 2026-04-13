using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(BoxCollider))]
public class SettlementZone : NetworkBehaviour
{
    private bool isTransitioning = false;
    public Transform anchor;

    public Transform deliveryDropPoint; // 트럭 외부 (상점템+퀘스트템 스폰 위치)
    public float dropRadius = 2.0f;     // 아이템이 흩뿌려질 반경

    private void Start() => SpawnItems();

    public void ExecuteTransition(PlayerInventory player, string targetScene, bool doSettlement)
    {
        // 이미 이동 중이거나 스폰되지 않았으면 무시 (연타 방지)
        if (!IsSpawned || isTransitioning) return;

        string cleanedScene = targetScene.Trim();
        Debug.Log($"<color=cyan><b>[Ship System]</b> 이동 요청 접수 (목적지: {cleanedScene} / 정산여부: {doSettlement})</color>");

        // 자물쇠 잠그기
        isTransitioning = true;

        if (IsServer)
        {
            PerformTransitionLogic(player, cleanedScene, doSettlement);
        }
        else
        {
            RequestTransitionServerRpc(cleanedScene, doSettlement);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestTransitionServerRpc(string targetScene, bool doSettlement, RpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;
        var playerObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        var playerInv = playerObj.GetComponent<PlayerInventory>();

        PerformTransitionLogic(playerInv, targetScene, doSettlement);
    }

    private void PerformTransitionLogic(PlayerInventory callerPlayer, string targetScene, bool doSettlement)
    {
        // ==========================================================
        // 💡 [절대 방어선] 필수 매니저들이 씬에 제대로 있는지부터 검사합니다!
        // ==========================================================
        if (GameSessionManager.Instance == null || QuestManager.Instance == null || GameMaster.Instance == null)
        {
            Debug.LogError("<color=red><b>🚨 [치명적 오류] 씬 이동을 위한 필수 매니저가 씬에 없습니다!</b></color>");
            if (GameSessionManager.Instance == null) Debug.LogError("➡️ 원인: GameSessionManager가 씬에 없음");
            if (QuestManager.Instance == null) Debug.LogError("➡️ 원인: QuestManager가 씬에 없음");
            if (GameMaster.Instance == null) Debug.LogError("➡️ 원인: GameMaster가 씬에 없음");

            isTransitioning = false; // 에러가 났으니 다음 번에 다시 누를 수 있게 자물쇠 풀기
            return; // 💥 튕기기 전에 함수 강제 종료
        }

        if (GameMaster.Instance.economyManager == null || GameMaster.Instance.dayCycleManager == null)
        {
            Debug.LogError("<color=red><b>🚨 GameMaster 하위에 EconomyManager 또는 DayCycleManager가 연결되지 않았습니다!</b></color>");
            isTransitioning = false;
            return;
        }

        // ==========================================================
        // 여기서부터는 안전이 보장된 상태로 정산 로직이 돌아갑니다.
        // ==========================================================
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
            int questIncome = QuestManager.Instance.GetCalculatedQuestReward();
            int finalDailyIncome = totalScrapValue + questIncome;

            int missingPhones = Mathf.Max(0, GameSessionManager.Instance.deadPlayersCount - recoveredPhonesCount);
            float penaltyMultiplier = 1.0f - (missingPhones * 0.05f);
            int finalNetIncome = Mathf.RoundToInt(finalDailyIncome * penaltyMultiplier);

            bool isWipedOut = GameSessionManager.Instance.deadPlayersCount >= GameSessionManager.Instance.totalPlayersInSession;

            // 중앙 통제실 보고
            GameMaster.Instance.EndDay(isWipedOut, finalNetIncome);

            // 퀘스트 초기화
            QuestManager.Instance.ResetDailyQuests();
        }

        // 마지막 씬 이동
        NetworkManager.Singleton.SceneManager.LoadScene(targetScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
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

        foreach (var d in GameSessionManager.Instance.truckItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(d.itemID);
            if (prefab == null || anchor == null) continue;
            ItemBase spawned = Instantiate(prefab, anchor.TransformPoint(d.localPos), anchor.rotation * d.localRot);
            if (spawned is Item_Durability dur) dur.currentDurability = d.stateValue1;
            spawned.GetComponent<NetworkObject>().Spawn();
        }

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
        }
        else
        {
            Debug.LogWarning("[SettlementZone] 택배를 내릴 deliveryDropPoint가 지정되지 않았습니다!");
        }

        GameSessionManager.Instance.pendingSpawnItemIDs.Clear();
    }

    private void OnDrawGizmos()
    {
        if (deliveryDropPoint != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawSphere(deliveryDropPoint.position, dropRadius);
        }
    }
}