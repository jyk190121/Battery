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
    public Transform safeDropPoint;

    [SerializeField] private List<ItemSpawnPoint> areaManagers = new List<ItemSpawnPoint>();

    // [추가] 생성된 아이템 추적 리스트 (디스폰용)
    private List<NetworkObject> spawnedItems = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (GameMaster.Instance != null)
            {
                GameMaster.Instance.OnDayStarted += HandleDayStarted;
                GameMaster.Instance.StartDay(); // 🔥 이거 없으면 작동안함 (유지)
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

    private void HandleDayStarted(int difficulty)
    {
        // [추가] 아침 시작 시 이전 아이템 청소
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

    // [추가] 이전 아이템 청소 로직
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

        // ==========================================================
        // 1. 수집 퀘스트 아이템 생성
        // ==========================================================
        if (QuestManager.Instance != null)
        {
            foreach (int activeQuestID in QuestManager.Instance.activeQuests)
            {
                QuestDataSO questData = QuestManager.Instance.GetQuestData(activeQuestID);

                if (questData != null && questData.targetItemID != 0)
                {
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(i => i.itemID == questData.targetItemID);

                    if (targetItemData != null)
                    {
                        // [추가] 금고 퀘스트 기믹
                        if (activeQuestID == 1000 || activeQuestID == 2000 || activeQuestID == 3000)
                        {
                            if (safeDropPoint != null)
                            {
                                SpawnObject(targetItemData, safeDropPoint.position, safeDropPoint.rotation);
                            }
                            continue;
                        }

                        if (TrySpawnSpecificItem(targetItemData, spawnDict)) successCount++;
                    }
                }
            }
        }

        // ==========================================================
        // 2. 열쇠 아이템
        // ==========================================================
        var keyItems = itemDatabase.Where(i => !string.IsNullOrEmpty(i.keyID)).OrderBy(x => Random.value).ToList();
        foreach (var keyItem in keyItems)
        {
            if (Random.Range(0f, 100f) <= keySpawnChance)
            {
                if (TrySpawnSpecificItem(keyItem, spawnDict)) successCount++;
            }
        }

        // ==========================================================
        // 3. 일반 폐지
        // ==========================================================
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
                ItemDataSO randomData = normalItems[Random.Range(0, normalItems.Count)];

                if (TrySpawnSpecificItem(randomData, spawnDict))
                {
                    successCount++;
                }
            }
            Debug.Log($"[Spawner] {successCount}/{targetSpawnCount}개 스폰 완료.");
        }
    }

    bool TrySpawnSpecificItem(ItemDataSO data, Dictionary<SpawnLocation, List<Transform>> dict)
    {
        if (dict.TryGetValue(data.spawnLocation, out List<Transform> points) && points.Count > 0)
        {
            int idx = Random.Range(0, points.Count);
            Transform target = points[idx];

            // [변경] Null 방어 및 추적 리스트가 적용된 별도 함수 호출
            SpawnObject(data, target.position, target.rotation);

            points.RemoveAt(idx);
            return true;
        }
        return false;
    }

    // [추가] Null 방어 및 아이템 추적이 포함된 안전한 생성 로직
    void SpawnObject(ItemDataSO data, Vector3 pos, Quaternion rot)
    {
        if (data == null || data.itemPrefab == null) return;

        GameObject obj = Instantiate(data.itemPrefab, pos, rot);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
            spawnedItems.Add(netObj); // 청소 리스트 등록
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