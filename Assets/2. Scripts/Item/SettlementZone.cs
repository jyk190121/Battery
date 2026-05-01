using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
        zoneCol.isTrigger = true; //실시간 감지를 위해 반드시 켜주세요
    }

    private void Start() => SpawnItems();

    // 수집 퀘스트 트럭 입출고 감지
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // ItemBase 전체가 아니라 처음부터 Item_Quest인지 콕 집어서 검사합니다.
        QuestCollectionItem questItem = other.GetComponentInParent<QuestCollectionItem>();

        if (questItem != null)
        {
            int qId = questItem.itemData.itemID;
            if (!QuestManager.Instance.itemsInTruck.Contains(qId))
                QuestManager.Instance.itemsInTruck.Add(qId);

            // 변경된 내용 전체 체크.
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

            // 팀원 UI를 위한 해제 신호 발생
            QuestManager.Instance.NotifyLocalClientToggleClientRpc(qId, false, RpcTarget.Everyone);
        }
    }


    //씬 이동 및 최종 정산 실행 (버튼/상호작용 진입점)
    public void ExecuteTransition(PlayerInventory player, string targetScene, bool doSettlement)
    {
        if (!IsSpawned || isTransitioning) return;

        string cleanedScene = targetScene.Trim();
        Debug.Log($"<color=cyan>[Ship System]</color> 이동 요청 접수 (목적지: {cleanedScene} / 정산여부: {doSettlement})");

        if (IsServer)
            StartCoroutine(PerformTransitionSequence(cleanedScene, doSettlement));
        else
            RequestTransitionServerRpc(cleanedScene, doSettlement);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTransitionServerRpc(string targetScene, bool doSettlement, RpcParams rpcParams = default)
    {
        StartCoroutine(PerformTransitionSequence(targetScene, doSettlement));
    }

    //실제 정산 및 이동 시퀀스 (코루틴)
    private IEnumerator PerformTransitionSequence(string targetScene, bool doSettlement)
    {
        if (!IsServer || isTransitioning) yield break;
        isTransitioning = true;

        if (GameSessionManager.Instance == null || QuestManager.Instance == null || GameMaster.Instance == null)
        {
            Debug.LogError("<color=red>[치명적 오류] 씬 이동을 위한 필수 매니저 없음!</color>");
            isTransitioning = false;
            yield break;
        }

        GameSessionManager.Instance.truckItems.Clear();
        GameSessionManager.Instance.playerItems.Clear();

        //1. OverlapBox로 현재 트럭 안의 생존자와 아이템 스냅샷
        Vector3 center = transform.position + transform.TransformDirection(zoneCol.center);
        Vector3 halfExtents = Vector3.Scale(zoneCol.size, transform.lossyScale) * 0.5f;
        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation);

        int totalScrapValue = 0;
        int recoveredPhonesCount = 0;
        List<PlayerInventory> playersInTruck = new List<PlayerInventory>();
        List<ulong> survivorIds = new List<ulong>();

        //트럭 바닥 & 플레이어 상태 스캔
        foreach (var t in targets)
        {
            ItemBase item = t.GetComponentInParent<ItemBase>();
            if (item != null && !item.isEquipped)
            {
                if (doSettlement)
                {
                    if (item.itemData.category == ItemCategory.Scrap)
                        totalScrapValue += (item is Item_Scrap scrap) ? scrap.currentScrapValue : item.itemData.basePrice;
                    else if (item.itemData.category == ItemCategory.Quest)
                        QuestManager.Instance.NotifyFinalClear(item.itemData.itemID, NetworkManager.ServerClientId);
                    else if (item.itemData.category == ItemCategory.Phone)
                        recoveredPhonesCount++;
                    else
                        SaveToTruck(item);
                }
                else SaveToTruck(item);

                if (doSettlement && (item.itemData.category == ItemCategory.Scrap || item.itemData.category == ItemCategory.Quest || item.itemData.category == ItemCategory.Phone))
                {
                    if (item.NetworkObject != null && item.NetworkObject.IsSpawned)
                        item.NetworkObject.Despawn();
                }
            }

            PlayerInventory p = t.GetComponentInParent<PlayerInventory>();
            if (p != null && !playersInTruck.Contains(p))
            {
                playersInTruck.Add(p);
                survivorIds.Add(p.OwnerClientId); //생존자 ID 확보
            }
        }

        //인벤토리 아이템 처리 (단축키 슬롯 및 💡양손 무기 모두 정산)
        foreach (var p in playersInTruck)
        {
            // [1] 단축키 슬롯 정산
            for (int i = 0; i < p.slots.Length; i++)
            {
                ItemBase slotItem = p.slots[i];
                if (slotItem != null)
                {
                    if (doSettlement)
                    {
                        if (slotItem.itemData.category == ItemCategory.Scrap)
                            totalScrapValue += (slotItem is Item_Scrap s) ? s.currentScrapValue : slotItem.itemData.basePrice;
                        else if (slotItem.itemData.category == ItemCategory.Quest)
                            QuestManager.Instance.NotifyFinalClear(slotItem.itemData.itemID, p.OwnerClientId);
                        else if (slotItem.itemData.category == ItemCategory.Phone)
                            recoveredPhonesCount++;
                        else SaveToPlayer(slotItem, i, p.OwnerClientId);
                    }
                    else SaveToPlayer(slotItem, i, p.OwnerClientId);

                    if (doSettlement && (slotItem.itemData.category == ItemCategory.Scrap || slotItem.itemData.category == ItemCategory.Quest || slotItem.itemData.category == ItemCategory.Phone))
                    {
                        if (slotItem.NetworkObject != null && slotItem.NetworkObject.IsSpawned)
                            slotItem.NetworkObject.Despawn();
                    }
                    p.slots[i] = null;
                }
            }

            // [2] 양손 아이템 정산 (💡 누락되었던 로직 추가됨)
            if (p.twoHandedItem != null)
            {
                ItemBase tItem = p.twoHandedItem;
                if (doSettlement)
                {
                    if (tItem.itemData.category == ItemCategory.Scrap)
                        totalScrapValue += (tItem is Item_Scrap s) ? s.currentScrapValue : tItem.itemData.basePrice;
                    else if (tItem.itemData.category == ItemCategory.Quest)
                        QuestManager.Instance.NotifyFinalClear(tItem.itemData.itemID, p.OwnerClientId);
                    else if (tItem.itemData.category == ItemCategory.Phone)
                        recoveredPhonesCount++;
                    else SaveToPlayer(tItem, -1, p.OwnerClientId);
                }
                else SaveToPlayer(tItem, -1, p.OwnerClientId);

                if (doSettlement && (tItem.itemData.category == ItemCategory.Scrap || tItem.itemData.category == ItemCategory.Quest || tItem.itemData.category == ItemCategory.Phone))
                {
                    if (tItem.NetworkObject != null && tItem.NetworkObject.IsSpawned)
                        tItem.NetworkObject.Despawn();
                }

                p.twoHandedItem = null;
                p.OnTwoHandedToggled?.Invoke(false);
            }
        }

        GameSessionManager.Instance.CleanupAllItemsInScene();

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif

        //2. [지연 보고 핵심] 생존자들에게 사진 제출 명령
        if (doSettlement && QuestCameraBridge.Instance != null)
        {
            QuestCameraBridge.Instance.CommandSubmitDataClientRpc(survivorIds.ToArray());

            // 💡 클라이언트들의 앨범 데이터가 서버로 도달할 찰나의 시간 확보 (0.15f -> 1.0f로 증가)
            yield return new WaitForSeconds(1.0f);
        }

        //3. 정산 금액 산정 및 게임 마스터 보고
        if (doSettlement)
        {
            try
            {
                var (questIncome, questScore) = QuestManager.Instance.GetCalculatedQuestResults();

                //최종 결산 로그 =========================================================================
                int totalQuests = QuestManager.Instance.activeQuests.Count;
                int clearedQuests = QuestManager.Instance.serverCompletedQuests.Count;

                Debug.Log($"<color=cyan><b>[최종 퀘스트 결산]</b></color> " +
                          $"총 {totalQuests}개 중 <color=lime>{clearedQuests}개</color> 클리어 완료! " +
                          $"(획득 자금: {questIncome} / 획득 실적: {questScore}pt)");
                // =========================================================================

                int finalDailyIncome = totalScrapValue + questIncome;

                int deadCount = GameSessionManager.Instance.deadPlayersCount;
                Debug.Log($"<color=white>[Settlement Debug]</color> 현재 씬 사망자: <color=red>{deadCount}명</color> | 회수된 스마트폰: <color=lime>{recoveredPhonesCount}개</color>");

                int missingPhones = Mathf.Max(0, deadCount - recoveredPhonesCount);
                float penaltyMultiplier = 1.0f - (missingPhones * 0.05f);
                int finalNetIncome = Mathf.RoundToInt(finalDailyIncome * penaltyMultiplier);

                Debug.Log($"<color=yellow>[Settlement Result]</color> {deadCount}명이 사망하였고 {recoveredPhonesCount}개의 스마트폰 반납이 확인되었습니다. (최종 패널티 배율: {penaltyMultiplier * 100}%)");

                bool isWipedOut = deadCount >= GameSessionManager.Instance.GetTotalPlayers();

                GameMaster.Instance.EndDay(isWipedOut, finalNetIncome, questScore);

                QuestManager.Instance.ResetDailyQuests();
            }
            catch (System.Exception e) { Debug.LogWarning($"[Settlement] 정산 오류 무시: {e.Message}"); }
        }

        //4. 다음 씬으로 이동
        if (NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.LoadScene(targetScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        else
            isTransitioning = false;
    }

    //트럭에 아이템 저장
    private void SaveToTruck(ItemBase item)
    {
        GameSessionManager.Instance.truckItems.Add(new ItemSaveData { itemID = item.itemData.itemID, localPos = anchor.InverseTransformPoint(item.transform.position), localRot = Quaternion.Inverse(anchor.rotation) * item.transform.rotation, stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0, slotIndex = -1 });
    }

    //플레이어 인벤토리에 아이템 저장
    private void SaveToPlayer(ItemBase item, int index, ulong pId)
    {
        if (!GameSessionManager.Instance.playerItems.ContainsKey(pId)) GameSessionManager.Instance.playerItems[pId] = new List<ItemSaveData>();
        GameSessionManager.Instance.playerItems[pId].Add(new ItemSaveData { itemID = item.itemData.itemID, slotIndex = index, stateValue1 = (item is Item_Durability dur) ? dur.currentDurability : 0 });
    }

    //아이템 스폰 및 복구
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

        //씬에 다 뿌렸으므로 트럭 리스트를 비워줍니다. 
        //이걸 안 하면 다음 씬 이동 시 "이전 씬 아이템 + 현재 씬 아이템"이 합쳐져서 저장됩니다.
        GameSessionManager.Instance.truckItems.Clear();

        //상점/퀘스트 대기열 스폰 로직은 그대로 유지
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

    //플레이어 되살리기 로직 실행
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestReviveAllPlayersServerRpc()
    {
        if (!IsServer) return;

        foreach (var player in PlayerController.AllPlayers)
        {
            player.RevivePlayer();
        }
    }

    //디버그 기즈모 그리기
    private void OnDrawGizmos()
    {
        if (deliveryDropPoint != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawSphere(deliveryDropPoint.position, dropRadius);
        }
    }
}