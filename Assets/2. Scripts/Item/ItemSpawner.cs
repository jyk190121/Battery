using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class ItemSpawner : NetworkBehaviour
{
    public ItemDataSO[] itemDatabase;

    [Header("스폰 설정")]
    public int minSpawnCount = 21;  // 일반 폐지 최소 개수 유지
    public int maxSpawnCount = 27;  // 일반 폐지 최대 개수 유지
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

        if (this == null) return; // 파괴된 객체면 즉시 정지 (좀비 방어)
        ClearPreviousItems();
        RefreshSpawnPoints();

        if (areaManagers.Count == 0)
        {
            Debug.LogWarning("[Spawner] 현재 씬에 스폰 지점이 없어 스폰을 건너뜁니다.");
            return;
        }

        // 목표 일반 폐지 개수만 산정 (21~27 + 난이도)
        int randomBaseCount = Random.Range(minSpawnCount, maxSpawnCount + 1);
        int targetNormalSpawnCount = randomBaseCount + (difficulty * extraSpawnPerDifficulty);

        Debug.Log($"[Spawner] 아침이 밝았습니다! (난이도: {difficulty}) -> 목표 일반 폐지: {targetNormalSpawnCount}개");

        SpawnRandomItems(targetNormalSpawnCount);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        // 객체가 파괴될 때 무조건 이벤트 연결 고리를 끊음
        if (GameMaster.Instance != null)
        {
            GameMaster.Instance.OnDayStarted -= HandleDayStarted;
        }
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

    void SpawnRandomItems(int targetNormalSpawnCount)
    {
        if (itemDatabase == null || areaManagers.Count == 0) return;

        Dictionary<SpawnLocation, List<Transform>> spawnDict = new Dictionary<SpawnLocation, List<Transform>>();
        foreach (var manager in areaManagers)
        {
            if (!spawnDict.ContainsKey(manager.location))
                spawnDict[manager.location] = new List<Transform>();

            spawnDict[manager.location].AddRange(manager.GetPoints());
        }

        int questItemCount = 0;

        // 1. 수집 퀘스트 아이템 생성 (일반 폐지와 카운트 분리: +α 스폰)
        if (QuestManager.Instance != null)
        {
            foreach (int activeQuestID in QuestManager.Instance.activeQuests)
            {
                QuestDataSO questData = QuestManager.Instance.GetQuestData(activeQuestID);
                if (questData == null) continue;

                // [기믹 1] 금고
                if (activeQuestID == 1000 || activeQuestID == 2000 || activeQuestID == 3000)
                {
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(i => i.itemID == questData.targetItemID);
                    if (targetItemData != null && safeDropPoints[0] != null)
                    {
                        SpawnObject(targetItemData, safeDropPoints[0].position, safeDropPoints[0].rotation);
                        questItemCount++;
                    }
                    continue;
                }

                // [기믹 2] 발전기
                if (activeQuestID == 1010 || activeQuestID == 2010 || activeQuestID == 3010)
                {
                    ItemDataSO wireData = itemDatabase.FirstOrDefault(i => i.itemID == 905);
                    if (wireData != null)
                    {
                        int wireCount = questData.materialCount > 0 ? questData.materialCount : 1;
                        for (int j = 0; j < wireCount; j++)
                        {
                            if (TrySpawnSpecificItem(wireData, spawnDict)) questItemCount++;
                        }
                    }
                    continue;
                }

                // [기믹 3] 저주 동상
                if (activeQuestID == 1020 || activeQuestID == 2020 || activeQuestID == 3020)
                {
                    ItemDataSO statueData = itemDatabase.FirstOrDefault(i => i.itemID == 906);
                    if (statueData != null)
                    {
                        if (TrySpawnSpecificItem(statueData, spawnDict)) questItemCount++;
                    }
                    continue;
                }

                // [기믹 4] 일반 수집 퀘스트
                if (questData.targetItemID != 0)
                {
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(i => i.itemID == questData.targetItemID);
                    if (targetItemData != null)
                    {
                        int requiredAmount = questData.materialCount > 0 ? questData.materialCount : 1;
                        for (int j = 0; j < requiredAmount; j++)
                        {
                            if (TrySpawnSpecificItem(targetItemData, spawnDict)) questItemCount++;
                        }
                    }
                }
            }
        }

        // 2. 열쇠 아이템 (마찬가지로 +α 스폰)
        int keySpawnCount = 0;
        var keyItems = itemDatabase.Where(i => !string.IsNullOrEmpty(i.keyID)).OrderBy(x => Random.value).ToList();
        foreach (var keyItem in keyItems)
        {
            if (Random.Range(0f, 100f) <= keySpawnChance)
            {
                if (TrySpawnSpecificItem(keyItem, spawnDict)) keySpawnCount++;
            }
        }

        // 3. 일반 폐지 스폰 (순수하게 targetNormalSpawnCount 만큼 스폰)
        var normalItems = itemDatabase.Where(i =>
            string.IsNullOrEmpty(i.keyID) &&
            i.category != ItemCategory.Quest &&
            i.spawnLocation != SpawnLocation.ShopOnly).ToList();

        int normalSuccessCount = 0;

        if (normalItems.Count > 0)
        {
            // 퀘스트 아이템 스폰 횟수를 차감하지 않음
            int remainingSpawns = targetNormalSpawnCount;

            Dictionary<SpawnLocation, int> quotas = new Dictionary<SpawnLocation, int>();

            // 3-1. 특수룸 할당 (각 방마다 1~2개)
            SpawnLocation[] specialRooms = { SpawnLocation.ScienceRoom, SpawnLocation.PrincipalRoom, SpawnLocation.ArtRoom, SpawnLocation.Infirmary, SpawnLocation.MusicRoom };

            foreach (var room in specialRooms)
            {
                if (spawnDict.ContainsKey(room) && spawnDict[room].Count > 0)
                {
                    int roomQuota = Random.Range(1, 3);
                    roomQuota = Mathf.Min(roomQuota, spawnDict[room].Count);
                    quotas[room] = roomQuota;
                    remainingSpawns -= roomQuota;
                }
            }

            // 3-2. 일반 층 할당 (남은 횟수를 균등 분배)
            SpawnLocation[] floors = { SpawnLocation.Floor1, SpawnLocation.Floor2, SpawnLocation.Floor3 };
            int spawnsPerFloor = Mathf.Max(0, remainingSpawns / 3);
            int leftover = Mathf.Max(0, remainingSpawns % 3);

            foreach (var floor in floors)
            {
                if (spawnDict.ContainsKey(floor) && spawnDict[floor].Count > 0)
                {
                    int floorQuota = spawnsPerFloor;
                    if (leftover > 0) { floorQuota++; leftover--; }
                    floorQuota = Mathf.Min(floorQuota, spawnDict[floor].Count);
                    quotas[floor] = floorQuota;
                }
            }

            // 3-3. 할당된 수량 스폰 실행
            foreach (var kvp in quotas)
            {
                SpawnLocation targetZone = kvp.Key;
                int amountToSpawn = kvp.Value;

                for (int j = 0; j < amountToSpawn; j++)
                {
                    var validItems = normalItems.Where(i => i.spawnLocation == targetZone).ToList();

                    if (targetZone == SpawnLocation.Floor1 || targetZone == SpawnLocation.Floor2 || targetZone == SpawnLocation.Floor3)
                    {
                        validItems.AddRange(normalItems.Where(i => i.spawnLocation == SpawnLocation.AllFloor));
                    }

                    // [안전망] 해당 구역용 아이템이 없으면 AllFloor로 대체
                    if (validItems.Count == 0)
                    {
                        validItems = normalItems.Where(i => i.spawnLocation == SpawnLocation.AllFloor).ToList();
                    }

                    if (validItems.Count > 0)
                    {
                        ItemDataSO selectedData = validItems[Random.Range(0, validItems.Count)];

                        if (spawnDict.TryGetValue(targetZone, out List<Transform> points) && points.Count > 0)
                        {
                            int idx = Random.Range(0, points.Count);
                            Transform targetPoint = points[idx];

                            SpawnObject(selectedData, targetPoint.position, targetPoint.rotation);
                            points.RemoveAt(idx);
                            normalSuccessCount++;
                        }
                    }
                }
            }

            // 명확한 디버그 로그 출력
            Debug.Log($"<color=cyan>[Spawner]</color> 스폰 결산 -> 퀘스트/열쇠: {questItemCount + keySpawnCount}개 + 일반 폐지: {normalSuccessCount}/{targetNormalSpawnCount}개. 총 맵 스폰: {questItemCount + keySpawnCount + normalSuccessCount}개");
        }
    }

    bool TrySpawnSpecificItem(ItemDataSO data, Dictionary<SpawnLocation, List<Transform>> dict)
    {
        List<Transform> candidatePoints = null;

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
        // 게임 실행 중이 아니고, 객체가 살아있을 때만 실행
        if (!Application.isPlaying && this != null)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
        Debug.Log("[Spawner] 지역 관리자 동기화 완료.");
    }
}