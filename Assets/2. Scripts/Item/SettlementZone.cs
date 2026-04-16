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
        // 💡 [방어 코드] 서버에서만 실행되도록 보장
        if (!IsServer) return;

        // [절대 방어선] 필수 매니저들이 씬에 제대로 있는지부터 검사
        if (GameSessionManager.Instance == null || QuestManager.Instance == null || GameMaster.Instance == null)
        {
            Debug.LogError("<color=red><b>🚨 [치명적 오류] 씬 이동을 위한 필수 매니저가 씬에 없습니다!</b></color>");
            isTransitioning = false;
            return;
        }

        if (GameMaster.Instance.economyManager == null || GameMaster.Instance.dayCycleManager == null)
        {
            Debug.LogError("<color=red><b>🚨 GameMaster 하위에 EconomyManager 또는 DayCycleManager가 연결되지 않았습니다!</b></color>");
            isTransitioning = false;
            return;
        }

        // ==========================================================
        // 💡 [중복 방지] 저장하기 전, 기존에 담겨있던 명단을 완전히 비웁니다.
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

        // 1. 트럭 바닥에 있는 아이템 처리
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
                else
                {
                    SaveToTruck(item);
                }

                // 정산된 아이템은 제거
                if (doSettlement && (item.itemData.category == ItemCategory.Scrap || item.itemData.category == ItemCategory.Quest || item.itemData.category == ItemCategory.Phone))
                {
                    if (item.NetworkObject != null && item.NetworkObject.IsSpawned) item.NetworkObject.Despawn();
                }
            }

            PlayerInventory p = t.GetComponentInParent<PlayerInventory>();
            if (p != null && !playersInTruck.Contains(p)) playersInTruck.Add(p);
        }

        // 2. 플레이어 인벤토리 아이템 처리
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

                    // 정산 품목은 인벤토리에서도 제거(Despawn)
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

        // 3. 씬에 남아있는 모든 잔여 네트워크 아이템 청소 (중복 생성 방지 핵심)
        ItemBase[] allItemsInScene = FindObjectsByType<ItemBase>(FindObjectsSortMode.None);
        foreach (var leftoverItem in allItemsInScene)
        {
            if (leftoverItem != null && leftoverItem.NetworkObject != null && leftoverItem.NetworkObject.IsSpawned)
                leftoverItem.NetworkObject.Despawn();
        }

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif

        // 4. 정산 결과 계산 및 보고
        if (doSettlement)
        {
            try
            {
                int questIncome = QuestManager.Instance.GetCalculatedQuestReward();
                int finalDailyIncome = totalScrapValue + questIncome;

                int missingPhones = Mathf.Max(0, GameSessionManager.Instance.deadPlayersCount - recoveredPhonesCount);
                float penaltyMultiplier = 1.0f - (missingPhones * 0.05f);
                int finalNetIncome = Mathf.RoundToInt(finalDailyIncome * penaltyMultiplier);

                bool isWipedOut = GameSessionManager.Instance.deadPlayersCount >= GameSessionManager.Instance.GetTotalPlayers();

                GameMaster.Instance.EndDay(isWipedOut, finalNetIncome);
                QuestManager.Instance.ResetDailyQuests();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Settlement] 정산 계산 중 오류 발생 (무시하고 이동): {e.Message}");
            }
        }

        // 5. 💡 최종 씬 이동
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

        Debug.Log($"<color=lime>[SettlementZone]</color> 아이템 복구 시작. 남은 짐 개수: {GameSessionManager.Instance.truckItems.Count}");

        foreach (var d in GameSessionManager.Instance.truckItems)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(d.itemID);
            if (prefab == null || anchor == null) continue;

            ItemBase spawned = Instantiate(prefab, anchor.TransformPoint(d.localPos), anchor.rotation * d.localRot);
            if (spawned is Item_Durability dur) dur.currentDurability = d.stateValue1;

            spawned.GetComponent<NetworkObject>().Spawn();
        }

        // 씬에 다 뿌렸으므로 트럭 리스트를 비워줍니다. 
        // 이걸 안 하면 다음 씬 이동 시 "이전 씬 아이템 + 현재 씬 아이템"이 합쳐져서 저장됩니다.
        GameSessionManager.Instance.truckItems.Clear();

        // (상점/퀘스트 대기열 스폰 로직은 그대로 유지)
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

    // 플레이어 되살리기 로직 실행
    [ServerRpc(RequireOwnership = false)]
    public void RequestReviveAllPlayersServerRpc()
    {
        if (!IsServer) return;

        foreach (var player in PlayerController.AllPlayers)
        {
            player.RevivePlayer();
        }
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