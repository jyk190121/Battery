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

    [SerializeField] private List<ItemSpawnPoint> areaManagers = new List<ItemSpawnPoint>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (GameMaster.Instance != null)
            {
                GameMaster.Instance.OnDayStarted += HandleDayStarted;
                GameMaster.Instance.StartDay();
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
        RefreshSpawnPoints();

        if (areaManagers.Count == 0)
        {
            Debug.LogWarning("[Spawner] 현재 씬에 스폰 지점이 없어 스폰을 건너뜁니다.");
            return;
        }

        int dynamicSpawnCount = baseSpawnCount + (difficulty * extraSpawnPerDifficulty);
        Debug.Log($"[Spawner] 아침이 밝았습니다! (난이도: {difficulty}) -> 총 {dynamicSpawnCount}개의 폐지를 스폰합니다.");

        SpawnRandomItems(dynamicSpawnCount);
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
        // 1. 수집 퀘스트 아이템 무조건 생성 (확정 스폰)
        // ==========================================================
        if (QuestManager.Instance != null)
        {
            // 현재 활성화된 퀘스트 ID들을 순회
            foreach (int activeQuestID in QuestManager.Instance.activeQuests)
            {
                QuestDataSO questData = QuestManager.Instance.GetQuestData(activeQuestID);

                // 해당 퀘스트가 '수집(Collect)'이나 '환원(Return)' 타입이고, 목표 아이템이 있다면
                if (questData != null && questData.targetItemID != 0)
                {
                    // DB에서 해당 아이템 프리팹을 찾음
                    ItemDataSO targetItemData = itemDatabase.FirstOrDefault(i => i.itemID == questData.targetItemID);

                    if (targetItemData != null)
                    {
                        //  수집1 금고 퀘스트는 여기서 스폰하지 않고 패스합니다!
                        if (activeQuestID == 1000 || activeQuestID == 2000 || activeQuestID == 3000)
                        {
                            continue; // 아래 스폰 로직을 건너뜀
                        }

                        if (TrySpawnSpecificItem(targetItemData, spawnDict)) successCount++;
                    }
                }
            }
        }
        // ==========================================================
        // 2. 열쇠 아이템 확률적 생성 (단일 변수 확률, 중복 불가)
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
        // 3. 나머지 일반 아이템 완전 무작위 생성 (가중치 없음, 1/N 확률)
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

                // 피드백 반영: 가중치 없이 무조건 리스트에서 1/N로 랜덤 뽑기
                ItemDataSO randomData = normalItems[Random.Range(0, normalItems.Count)];

                if (TrySpawnSpecificItem(randomData, spawnDict))
                {
                    successCount++;
                }
            }
            Debug.Log($"[Spawner] {successCount}/{targetSpawnCount}개 스폰 완료. (시도: {attempts})");
        }
    }

    bool TrySpawnSpecificItem(ItemDataSO data, Dictionary<SpawnLocation, List<Transform>> dict)
    {
        if (dict.TryGetValue(data.spawnLocation, out List<Transform> points) && points.Count > 0)
        {
            int idx = Random.Range(0, points.Count);
            Transform target = points[idx];

            GameObject obj = Instantiate(data.itemPrefab, target.position, target.rotation);
            ItemBase item = obj.GetComponent<ItemBase>();
            if (item != null) item.itemData = data;

            HandleNetworkSpawn(obj);

            points.RemoveAt(idx);
            return true;
        }
        return false;
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

    private void HandleNetworkSpawn(GameObject obj)
    {
        if (IsServer) obj.GetComponent<NetworkObject>()?.Spawn();
    }
}