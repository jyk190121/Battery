using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class ItemSpawner : NetworkBehaviour
{
    public ItemDataSO[] itemDatabase;

    [Header("스폰 설정")]
    public int baseSpawnCount = 10;
    public int extraSpawnPerDifficulty = 2;

    [Header("확률 설정")]
    [Tooltip("각 열쇠가 스폰될 확률 (%)")]
    [Range(0f, 100f)]
    public float keySpawnChance = 15f;

    [Header("퀘스트 기믹 설정")]
    public Transform[] safeDropPoints = new Transform[3];

    [SerializeField] private List<ItemSpawnPoint> areaManagers = new List<ItemSpawnPoint>();

    private List<NetworkObject> spawnedItems = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (GameMaster.Instance != null)
            {
                GameMaster.Instance.OnDayStarted += HandleDayStarted;

                //다른 매니저들이 데이터를 셋업할 시간을 벌어준 뒤 스폰을 시작함
                StartCoroutine(DelayedStartDay());
            }
        }
    }

    private System.Collections.IEnumerator DelayedStartDay()
    {
        yield return new WaitForSeconds(0.5f);
        GameMaster.Instance.StartDay();
    }
    public override void OnNetworkDespawn()
    {
        if (IsServer && GameMaster.Instance != null)
        {
            GameMaster.Instance.OnDayStarted -= HandleDayStarted;
        }
    }

    private void HandleDayStarted(int difficulty)
    {
        ClearPreviousItems();
        RefreshSpawnPoints();

        if (areaManagers.Count == 0)
        {
            Debug.LogWarning("[Spawner] 현재 씬에 스폰 지점이 없어 스폰을 건너뜁니다.");
            return;
        }

        int dynamicSpawnCount = baseSpawnCount + (difficulty * extraSpawnPerDifficulty);
        Debug.Log($"[Spawner] 아침이 밝았습니다! (난이도: {difficulty}) -> 총 {dynamicSpawnCount}개의 아이템을 스폰합니다.");

        SpawnRandomItems(dynamicSpawnCount);
    }

    private void ClearPreviousItems()
    {
        foreach (var netObj in spawnedItems)
        {
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }
        spawnedItems.Clear();
    }

    void SpawnRandomItems(int targetSpawnCount)
    {
        if (itemDatabase == null || areaManagers.Count == 0) return;

        Dictionary<SpawnLocation, List<Transform>> spawnDict = new Dictionary<SpawnLocation, List<Transform>>();
        foreach (var manager in areaManagers)
        {
            if (!spawnDict.ContainsKey(manager.location))
                spawnDict[manager.location] = new List<Transform>();

            spawnDict[manager.location].AddRange(manager.GetPoints());
        }

        int successCount = 0;

        // 1. 수집 퀘스트 아이템 생성
        if (QuestManager.Instance != null)
        {
            foreach (int activeQuestID in QuestManager.Instance.activeQuests)
            {
                QuestDataSO questData = QuestManager.Instance.GetQuestData(activeQuestID);
                if (questData == null) continue;

                // 금고 퀘스트 (1000: Easy, 2000: Normal, 3000: Hard)
                if (activeQuestID == 1000 || activeQuestID == 2000 || activeQuestID == 3000)
                {
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(i => i.itemID == questData.targetItemID);
                    if (targetItemData != null)
                    {
                        // 2~3층 금고 미구현 대응: 난이도 상관없이 무조건 1층 금고(인덱스 0) 사용
                        Transform targetSafe = safeDropPoints[0];

                        /* --- [향후 2~3층 금고 구현 시 아래 주석 해제 및 적용] ---
                        if (activeQuestID == 1000 && safeDropPoints[0] != null) targetSafe = safeDropPoints[0];      // 1층
                        else if (activeQuestID == 2000 && safeDropPoints[1] != null) targetSafe = safeDropPoints[1]; // 2층
                        else if (activeQuestID == 3000 && safeDropPoints[2] != null) targetSafe = safeDropPoints[2]; // 3층
                        ------------------------------------------------------------------------- */

                        if (targetSafe != null)
                        {
                            SpawnObject(targetItemData, targetSafe.position, targetSafe.rotation);
                        }
                        else
                        {
                            Debug.LogWarning($"<color=red>[Spawner]</color> 1층 금고(safeDropPoints[0]) Transform이 인스펙터에 할당되지 않았습니다!");
                        }
                    }
                    continue; // 처리 완료 후 다음 퀘스트로
                }

                // [기믹 2] 발전기 수리 퀘스트 (1010, 2010, 3010)
                if (activeQuestID == 1010 || activeQuestID == 2010 || activeQuestID == 3010)
                {
                    // 진짜 목표물(targetItemID)은 어댑터가 문 뒤에 스폰하므로 무시함.
                    // 대신 맵 전체에 '수리 부속(전선 905)'을 materialCount 만큼 뿌림.
                    ItemDataSO wireData = itemDatabase.FirstOrDefault(i => i.itemID == 905); // 905 고정 스폰
                    if (wireData != null)
                    {
                        int wireCount = questData.materialCount > 0 ? questData.materialCount : 1;
                        for (int j = 0; j < wireCount; j++)
                        {
                            if (TrySpawnSpecificItem(wireData, spawnDict))
                            {
                                successCount++;
                            }
                            else
                            {
                                Debug.LogWarning($"<color=red>[Spawner]</color> 발전기 부속(905) 스폰 실패! (공간 부족)");
                            }
                        }
                    }
                    continue; // 처리 완료 후 다음 퀘스트로
                }

                // [기믹 3] 일반 수집 퀘스트 (나머지)
                if (questData.targetItemID != 0)
                {
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(i => i.itemID == questData.targetItemID);
                    if (targetItemData != null)
                    {
                        int requiredAmount = questData.materialCount > 0 ? questData.materialCount : 1;
                        for (int j = 0; j < requiredAmount; j++)
                        {
                            if (TrySpawnSpecificItem(targetItemData, spawnDict))
                            {
                                successCount++;
                            }
                        }
                    }
                }
            }
        }

        // 2. 열쇠 아이템
        var keyItems = itemDatabase.Where(i => !string.IsNullOrEmpty(i.keyID)).OrderBy(x => Random.value).ToList();
        foreach (var keyItem in keyItems)
        {
            if (Random.Range(0f, 100f) <= keySpawnChance)
            {
                if (TrySpawnSpecificItem(keyItem, spawnDict)) successCount++;
            }
        }

        // 3. 일반 폐지 (AllFloor 및 잔여 스폰 지점 필터링 적용)
        var normalItems = itemDatabase.Where(i =>
            string.IsNullOrEmpty(i.keyID) &&
            i.category != ItemCategory.Quest &&
            i.spawnLocation != SpawnLocation.ShopOnly).ToList();

        if (normalItems.Count > 0)
        {
            int attempts = 0;
            int maxAttempts = targetSpawnCount * 3;

            while (successCount < targetSpawnCount && attempts < maxAttempts)
            {
                attempts++;

                // AllFloor 조건까지 포함하여 현재 씬에 꽂을 자리가 있는 아이템만 후보군으로 압축
                var validNormalItems = normalItems.Where(i =>
                {
                    if (i.spawnLocation == SpawnLocation.AllFloor)
                    {
                        return (spawnDict.ContainsKey(SpawnLocation.Floor1) && spawnDict[SpawnLocation.Floor1].Count > 0) ||
                               (spawnDict.ContainsKey(SpawnLocation.Floor2) && spawnDict[SpawnLocation.Floor2].Count > 0) ||
                               (spawnDict.ContainsKey(SpawnLocation.Floor3) && spawnDict[SpawnLocation.Floor3].Count > 0);
                    }
                    return spawnDict.ContainsKey(i.spawnLocation) && spawnDict[i.spawnLocation].Count > 0;
                }).ToList();

                if (validNormalItems.Count == 0)
                {
                    Debug.LogWarning($"[Spawner] 남은 자리에 맞는 일반 아이템 SO가 없어 스폰을 조기 종료합니다. ({successCount}/{targetSpawnCount})");
                    break;
                }

                ItemDataSO randomData = validNormalItems[Random.Range(0, validNormalItems.Count)];

                if (TrySpawnSpecificItem(randomData, spawnDict))
                {
                    successCount++;
                }
            }
            Debug.Log($"[Spawner] 최종 스폰 결과: {successCount}/{targetSpawnCount}개 스폰 완료.");
        }
    }

    bool TrySpawnSpecificItem(ItemDataSO data, Dictionary<SpawnLocation, List<Transform>> dict)
    {
        List<Transform> candidatePoints = null;

        // AllFloor일 경우 1~3층 중 자리가 남는 층을 무작위로 선택하여 꽂아넣음
        if (data.spawnLocation == SpawnLocation.AllFloor)
        {
            var availableFloors = new List<SpawnLocation> { SpawnLocation.Floor1, SpawnLocation.Floor2, SpawnLocation.Floor3 };
            var validFloors = availableFloors.Where(f => dict.ContainsKey(f) && dict[f].Count > 0).ToList();

            if (validFloors.Count > 0)
            {
                SpawnLocation selectedFloor = validFloors[Random.Range(0, validFloors.Count)];
                candidatePoints = dict[selectedFloor];
            }
        }
        else if (dict.TryGetValue(data.spawnLocation, out List<Transform> points) && points.Count > 0)
        {
            candidatePoints = points;
        }

        if (candidatePoints != null && candidatePoints.Count > 0)
        {
            int idx = Random.Range(0, candidatePoints.Count);
            Transform target = candidatePoints[idx];

            SpawnObject(data, target.position, target.rotation);

            candidatePoints.RemoveAt(idx);
            return true;
        }
        return false;
    }

    void SpawnObject(ItemDataSO data, Vector3 pos, Quaternion rot)
    {
        if (data == null || data.itemPrefab == null) return;

        GameObject obj = Instantiate(data.itemPrefab, pos, rot);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
            spawnedItems.Add(netObj);
        }

        ItemBase item = obj.GetComponent<ItemBase>();
        if (item != null) item.itemData = data;
    }

    [ContextMenu("Bake: 모든 지역 관리자 동기화")]
    public void RefreshSpawnPoints()
    {
        areaManagers.Clear();
        ItemSpawnPoint[] found = Object.FindObjectsByType<ItemSpawnPoint>(FindObjectsSortMode.None);

        foreach (var manager in found)
        {
            manager.UpdateChildPoints();
            areaManagers.Add(manager);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[Spawner] 지역 관리자 동기화 완료.");
    }
}