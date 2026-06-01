using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

/// <summary>
/// 매일 아침(Day Cycle) 시작 시 맵 전체에 폐지와 퀘스트 아이템을 무작위로 스폰하는 매니저.
/// </summary>
public class ItemSpawner : NetworkBehaviour
{
    public ItemDataSO[] itemDatabase;

    [Header("스폰 설정")]
    public int minSpawnCount = 21;
    public int maxSpawnCount = 27;
    public int extraSpawnPerDifficulty = 2;

    [Header("퀘스트 기믹 설정")]
    public Transform[] safeDropPoints = new Transform[3];

    [SerializeField] private List<ItemSpawnPoint> areaManagers = new List<ItemSpawnPoint>();
    private List<NetworkObject> spawnedItems = new List<NetworkObject>();


    // ===============
    // 1.이벤트 연결
    // ===============

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (GameMaster.Instance != null)
            {
                GameMaster.Instance.OnDayStarted += HandleDayStarted;
                StartCoroutine(DelayedStartDayRoutine());
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && GameMaster.Instance != null)
        {
            GameMaster.Instance.OnDayStarted -= HandleDayStarted;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (GameMaster.Instance != null)
        {
            GameMaster.Instance.OnDayStarted -= HandleDayStarted;
        }
    }

    private System.Collections.IEnumerator DelayedStartDayRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        GameMaster.Instance.StartDay();
    }


    // ==========================================
    // 2. 일일 스폰 로직 (Spawning Logic)
    // ==========================================

    private void HandleDayStarted(int difficulty)
    {
        if (this == null) { return; }

        ClearPreviousItems();
        RefreshSpawnPoints();

        if (areaManagers.Count == 0)
        {
            Debug.LogWarning("[Spawner] 현재 씬에 스폰 지점이 없어 스폰을 건너뜁니다.");
            return;
        }

        int randomBaseCount = Random.Range(minSpawnCount, maxSpawnCount + 1);
        int targetNormalSpawnCount = randomBaseCount + (difficulty * extraSpawnPerDifficulty);

        Debug.Log($"[Spawner] 아침이 밝았습니다! (난이도: {difficulty}) -> 목표 일반 폐지: {targetNormalSpawnCount}개");

        SpawnRandomItems(targetNormalSpawnCount);
    }

    private void ClearPreviousItems()
    {
        foreach (NetworkObject netObj in spawnedItems)
        {
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }
        spawnedItems.Clear();
    }

    private void SpawnRandomItems(int targetNormalSpawnCount)
    {
        if (itemDatabase == null || areaManagers.Count == 0) { return; }

        Dictionary<SpawnLocation, List<Transform>> spawnDictionary = new Dictionary<SpawnLocation, List<Transform>>();

        foreach (ItemSpawnPoint manager in areaManagers)
        {
            if (!spawnDictionary.ContainsKey(manager.location))
            {
                spawnDictionary[manager.location] = new List<Transform>();
            }
            spawnDictionary[manager.location].AddRange(manager.GetPoints());
        }

        int questItemCount = 0;

        if (QuestManager.Instance != null)
        {
            foreach (int activeQuestID in QuestManager.Instance.activeQuests)
            {
                QuestDataSO questData = QuestManager.Instance.GetQuestData(activeQuestID);
                if (questData == null) { continue; }

                if (activeQuestID == 1000 || activeQuestID == 2000 || activeQuestID == 3000)
                {
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(item => item.itemID == questData.targetItemID);
                    if (targetItemData != null && safeDropPoints[0] != null)
                    {
                        SpawnObject(targetItemData, safeDropPoints[0].position, safeDropPoints[0].rotation);
                        questItemCount++;
                    }
                    continue;
                }

                if (activeQuestID == 1010 || activeQuestID == 2010 || activeQuestID == 3010)
                {
                    ItemDataSO wireData = itemDatabase.FirstOrDefault(item => item.itemID == 905);
                    if (wireData != null)
                    {
                        int wireCount = questData.materialCount > 0 ? questData.materialCount : 1;
                        for (int count = 0; count < wireCount; count++)
                        {
                            if (TrySpawnSpecificItem(wireData, spawnDictionary))
                            {
                                questItemCount++;
                            }
                        }
                    }
                    continue;
                }

                if (activeQuestID == 1020 || activeQuestID == 2020 || activeQuestID == 3020)
                {
                    ItemDataSO statueData = itemDatabase.FirstOrDefault(item => item.itemID == 906);
                    if (statueData != null)
                    {
                        if (TrySpawnSpecificItem(statueData, spawnDictionary))
                        {
                            questItemCount++;
                        }
                    }
                    continue;
                }

                if (questData.targetItemID != 0)
                {
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(item => item.itemID == questData.targetItemID);
                    if (targetItemData != null)
                    {
                        int requiredAmount = questData.materialCount > 0 ? questData.materialCount : 1;
                        for (int count = 0; count < requiredAmount; count++)
                        {
                            if (TrySpawnSpecificItem(targetItemData, spawnDictionary))
                            {
                                questItemCount++;
                            }
                        }
                    }
                }
            }
        }

        List<ItemDataSO> normalItems = itemDatabase.Where(item =>
            item.category != ItemCategory.Quest &&
            item.spawnLocation != SpawnLocation.ShopOnly).ToList();

        int normalSuccessCount = 0;

        if (normalItems.Count > 0)
        {
            int remainingSpawns = targetNormalSpawnCount;
            Dictionary<SpawnLocation, int> spawnQuotas = new Dictionary<SpawnLocation, int>();

            SpawnLocation[] specialRooms = { SpawnLocation.ScienceRoom, SpawnLocation.PrincipalRoom, SpawnLocation.ArtRoom, SpawnLocation.Infirmary, SpawnLocation.MusicRoom };

            foreach (SpawnLocation room in specialRooms)
            {
                if (spawnDictionary.ContainsKey(room) && spawnDictionary[room].Count > 0)
                {
                    int roomQuota = Random.Range(1, 3);
                    roomQuota = Mathf.Min(roomQuota, spawnDictionary[room].Count);
                    spawnQuotas[room] = roomQuota;
                    remainingSpawns -= roomQuota;
                }
            }

            SpawnLocation[] floors = { SpawnLocation.Floor1, SpawnLocation.Floor2, SpawnLocation.Floor3 };
            int spawnsPerFloor = Mathf.Max(0, remainingSpawns / 3);
            int leftoverSpawns = Mathf.Max(0, remainingSpawns % 3);

            foreach (SpawnLocation floor in floors)
            {
                if (spawnDictionary.ContainsKey(floor) && spawnDictionary[floor].Count > 0)
                {
                    int floorQuota = spawnsPerFloor;
                    if (leftoverSpawns > 0)
                    {
                        floorQuota++;
                        leftoverSpawns--;
                    }
                    floorQuota = Mathf.Min(floorQuota, spawnDictionary[floor].Count);
                    spawnQuotas[floor] = floorQuota;
                }
            }

            foreach (KeyValuePair<SpawnLocation, int> quotaInfo in spawnQuotas)
            {
                SpawnLocation targetZone = quotaInfo.Key;
                int amountToSpawn = quotaInfo.Value;

                for (int spawnCount = 0; spawnCount < amountToSpawn; spawnCount++)
                {
                    List<ItemDataSO> validItems = normalItems.Where(item => item.spawnLocation == targetZone).ToList();

                    if (targetZone == SpawnLocation.Floor1 || targetZone == SpawnLocation.Floor2 || targetZone == SpawnLocation.Floor3)
                    {
                        validItems.AddRange(normalItems.Where(item => item.spawnLocation == SpawnLocation.AllFloor));
                    }

                    if (validItems.Count == 0)
                    {
                        validItems = normalItems.Where(item => item.spawnLocation == SpawnLocation.AllFloor).ToList();
                    }

                    if (validItems.Count > 0)
                    {
                        ItemDataSO selectedData = validItems[Random.Range(0, validItems.Count)];

                        if (spawnDictionary.TryGetValue(targetZone, out List<Transform> points) && points.Count > 0)
                        {
                            int pointIndex = Random.Range(0, points.Count);
                            Transform targetPoint = points[pointIndex];

                            SpawnObject(selectedData, targetPoint.position, targetPoint.rotation);
                            points.RemoveAt(pointIndex);
                            normalSuccessCount++;
                        }
                    }
                }
            }

            Debug.Log($"<color=cyan>[Spawner]</color> 스폰 결산 -> 퀘스트 아이템: {questItemCount}개 + 일반 폐지: {normalSuccessCount}/{targetNormalSpawnCount}개. 총 맵 스폰: {questItemCount + normalSuccessCount}개");
        }
    }


    // ========================
    // 3. 유틸리티 및 에디터 툴 
    // ========================

    private bool TrySpawnSpecificItem(ItemDataSO data, Dictionary<SpawnLocation, List<Transform>> spawnDictionary)
    {
        List<Transform> candidatePoints = null;

        if (data.spawnLocation == SpawnLocation.AllFloor)
        {
            List<SpawnLocation> availableFloors = new List<SpawnLocation> { SpawnLocation.Floor1, SpawnLocation.Floor2, SpawnLocation.Floor3 };
            List<SpawnLocation> validFloors = availableFloors.Where(floor => spawnDictionary.ContainsKey(floor) && spawnDictionary[floor].Count > 0).ToList();

            if (validFloors.Count > 0)
            {
                SpawnLocation selectedFloor = validFloors[Random.Range(0, validFloors.Count)];
                candidatePoints = spawnDictionary[selectedFloor];
            }
        }
        else if (spawnDictionary.TryGetValue(data.spawnLocation, out List<Transform> points) && points.Count > 0)
        {
            candidatePoints = points;
        }

        if (candidatePoints != null && candidatePoints.Count > 0)
        {
            int pointIndex = Random.Range(0, candidatePoints.Count);
            Transform target = candidatePoints[pointIndex];

            SpawnObject(data, target.position, target.rotation);

            candidatePoints.RemoveAt(pointIndex);
            return true;
        }
        return false;
    }

    private void SpawnObject(ItemDataSO data, Vector3 position, Quaternion rotation)
    {
        if (data == null || data.itemPrefab == null) { return; }

        GameObject obj = Instantiate(data.itemPrefab, position, rotation);
        NetworkObject networkObj = obj.GetComponent<NetworkObject>();

        if (networkObj != null)
        {
            networkObj.Spawn();
            spawnedItems.Add(networkObj);
        }

        ItemBase item = obj.GetComponent<ItemBase>();
        if (item != null)
        {
            item.itemData = data;
        }
    }

    [ContextMenu("Bake: 모든 지역 관리자 동기화")]
    public void RefreshSpawnPoints()
    {
        areaManagers.Clear();
        ItemSpawnPoint[] foundManagers = Object.FindObjectsByType<ItemSpawnPoint>(FindObjectsSortMode.None);

        foreach (ItemSpawnPoint manager in foundManagers)
        {
            manager.UpdateChildPoints();
            areaManagers.Add(manager);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && this != null)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
        Debug.Log("[Spawner] 지역 관리자 동기화 완료.");
    }
}