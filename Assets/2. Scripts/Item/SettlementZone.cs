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

    private BoxCollider zoneCol;

    private void Awake()
    {
        zoneCol = GetComponent<BoxCollider>();
        zoneCol.isTrigger = true;
    }

    private void Start() => SpawnItems();

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        QuestCollectionItem questItem = other.GetComponentInParent<QuestCollectionItem>();
        // 누군가 손에 들고 있는 아이템은 트럭 바닥 리스트에 넣지 않음 (인벤토리에서 별도 정산)
        if (questItem != null && !questItem.isEquipped)
        {
            int qId = questItem.itemData.itemID;
            if (!QuestManager.Instance.itemsInTruck.Contains(qId))
                QuestManager.Instance.itemsInTruck.Add(qId);

            QuestManager.Instance.NotifyLocalClientToggleClientRpc(qId, true, RpcTarget.Everyone);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        QuestCollectionItem questItem = other.GetComponentInParent<QuestCollectionItem>();

        if (questItem != null)
        {
            int qId = questItem.itemData.itemID;
            if (QuestManager.Instance.itemsInTruck.Contains(qId))
                QuestManager.Instance.itemsInTruck.Remove(qId);

            QuestManager.Instance.NotifyLocalClientToggleClientRpc(qId, false, RpcTarget.Everyone);
        }
    }

    public void ExecuteTransition(PlayerInventory player, string targetScene, bool doSettlement)
    {
        if (!IsSpawned || isTransitioning) return;

        string cleanedScene = targetScene.Trim();
        Debug.Log($"<color=cyan>[Ship System]</color> 이동 요청 접수 (목적지: {cleanedScene} / 정산여부: {doSettlement})");

        if (IsServer)
            StartCoroutine(PerformTransitionSequence(player, cleanedScene, doSettlement));
        else
            RequestTransitionServerRpc(player.OwnerClientId, cleanedScene, doSettlement);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTransitionServerRpc(ulong callerId, string targetScene, bool doSettlement, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(callerId, out var client))
        {
            PlayerInventory player = client.PlayerObject.GetComponent<PlayerInventory>();
            StartCoroutine(PerformTransitionSequence(player, targetScene, doSettlement));
        }
    }

    private IEnumerator PerformTransitionSequence(PlayerInventory caller, string targetScene, bool doSettlement)
    {
        if (!IsServer || isTransitioning) yield break;
        isTransitioning = true;

        if (GameSessionManager.Instance == null || QuestManager.Instance == null || GameMaster.Instance == null)
        {
            Debug.LogError("<color=red>[Error] Required Managers missing!</color>");
            isTransitioning = false;
            yield break;
        }

        GameSessionManager.Instance.truckItems.Clear();
        GameSessionManager.Instance.playerItems.Clear();

        Vector3 center = transform.position + transform.TransformDirection(zoneCol.center);
        Vector3 halfExtents = Vector3.Scale(zoneCol.size, transform.lossyScale) * 0.5f;
        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);

        int totalScrapValue = 0;
        int recoveredPhonesCount = 0;
        List<PlayerInventory> playersInTruck = new List<PlayerInventory>();
        List<ulong> survivorIds = new List<ulong>();

        // 버튼을 누른 사람은 박스 영역에서 살짝 벗어나 있어도 무조건 생존자로 취급
        if (caller != null && !survivorIds.Contains(caller.OwnerClientId))
        {
            playersInTruck.Add(caller);
            survivorIds.Add(caller.OwnerClientId);
        }

        // [Step A] 트럭 바닥 스캔
        foreach (var t in targets)
        {
            ItemBase item = t.GetComponentInParent<ItemBase>();
            if (item != null && !item.isEquipped)
            {
                if (doSettlement)
                {
                    if (item.itemData.category == ItemCategory.Scrap)
                        totalScrapValue += (item is Item_Scrap scrap) ? scrap.currentScrapValue : item.itemData.basePrice;
                    if (item.itemData.category == ItemCategory.Phone)
                        recoveredPhonesCount++;

                    QuestManager.Instance.NotifyFinalClear(item.itemData.itemID, NetworkManager.ServerClientId);
                }
                else SaveToTruck(item);
            }

            PlayerInventory p = t.GetComponentInParent<PlayerInventory>();
            if (p != null && !playersInTruck.Contains(p))
            {
                playersInTruck.Add(p);
                survivorIds.Add(p.OwnerClientId);
            }
        }

        // [Step B] 플레이어 인벤토리 스캔 및 동기화 소각
        foreach (var p in playersInTruck)
        {
            // 1. 단축키 슬롯
            for (int i = 0; i < p.slots.Length; i++)
            {
                ItemBase slotItem = p.slots[i];
                if (slotItem != null)
                {
                    if (doSettlement)
                    {
                        if (slotItem.itemData.category == ItemCategory.Scrap)
                            totalScrapValue += (slotItem is Item_Scrap s) ? s.currentScrapValue : slotItem.itemData.basePrice;
                        if (slotItem.itemData.category == ItemCategory.Phone)
                            recoveredPhonesCount++;

                        QuestManager.Instance.NotifyFinalClear(slotItem.itemData.itemID, p.OwnerClientId);

                        if (slotItem.itemData.category == ItemCategory.Scrap ||
                            slotItem.itemData.category == ItemCategory.Quest ||
                            slotItem.itemData.category == ItemCategory.Phone)
                        {
                            p.RemoveItemByServer(slotItem.itemData.itemID);
                            continue;
                        }
                    }
                    SaveToPlayer(slotItem, i, p.OwnerClientId);
                    p.slots[i] = null;
                }
            }

            // 2. 양손 아이템
            if (p.twoHandedItem != null)
            {
                ItemBase tItem = p.twoHandedItem;
                if (doSettlement)
                {
                    if (tItem.itemData.category == ItemCategory.Scrap)
                        totalScrapValue += (tItem is Item_Scrap s) ? s.currentScrapValue : tItem.itemData.basePrice;
                    if (tItem.itemData.category == ItemCategory.Phone)
                        recoveredPhonesCount++;

                    QuestManager.Instance.NotifyFinalClear(tItem.itemData.itemID, p.OwnerClientId);

                    if (tItem.itemData.category == ItemCategory.Scrap ||
                        tItem.itemData.category == ItemCategory.Quest ||
                        tItem.itemData.category == ItemCategory.Phone)
                    {
                        p.RemoveItemByServer(tItem.itemData.itemID);
                        continue;
                    }
                }
                SaveToPlayer(tItem, -1, p.OwnerClientId);
                p.twoHandedItem = null;
                p.OnTwoHandedToggled?.Invoke(false);
            }
        }

        // [Step C] 트럭 트리거(itemsInTruck) 최종 확인 및 기록
        if (doSettlement)
        {
            foreach (int itemId in QuestManager.Instance.itemsInTruck)
            {
                QuestManager.Instance.NotifyFinalClear(itemId, NetworkManager.ServerClientId);
            }
        }

        GameSessionManager.Instance.CleanupAllItemsInScene();

        // [Step D] 사진 데이터 수집 (RPC 대기)
        if (doSettlement && QuestCameraBridge.Instance != null)
        {
            QuestCameraBridge.Instance.CommandSubmitDataClientRpc(survivorIds.ToArray());
            yield return new WaitForSeconds(1.0f); // 1.0f 절대 유지 (RPC 수신 및 장부 갱신 시간)
        }

        // [Step E] 최종 결산
        if (doSettlement)
        {
            try
            {
                var (questIncome, questScore) = QuestManager.Instance.GetCalculatedQuestResults();

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
                GameMaster.Instance.EndDay(isWipedOut, finalNetIncome, questScore);
                QuestManager.Instance.ResetDailyQuests();
            }
            catch (System.Exception e) { Debug.LogWarning($"[Settlement] Error: {e.Message}"); }
        }

        // [Step F] 씬 로드
        if (NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.LoadScene(targetScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        else
            isTransitioning = false;
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
        if (!IsServer) return;
        foreach (var player in PlayerController.AllPlayers)
            player.RevivePlayer();
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