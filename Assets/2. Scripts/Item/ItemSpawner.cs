using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ItemSpawner : NetworkBehaviour
{
    public ItemDataSO[] itemDatabase;

    [Header("스폰 설정")]
    public int baseSpawnCount = 10; // 1일차 기본 스폰 개수
    public int extraSpawnPerDifficulty = 2; // 난이도 1당 추가로 스폰할 개수

    [SerializeField] private List<ItemSpawnPoint> areaManagers = new List<ItemSpawnPoint>();

    // 씬 시작 시: GameMaster 구독
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

    // 씬 종료 시: 메모리 누수 및 중복 실행을 막기 위해 반드시 구독 해제
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (GameMaster.Instance != null)
            {
                GameMaster.Instance.OnDayStarted -= HandleDayStarted;
            }
        }
    }

    //아침이 밝으면 GameMaster가 이 함수를 자동으로 실행
    private void HandleDayStarted(int difficulty)
    {
        // 공식: 기본 개수 + (난이도 * 추가 배율)
        int dynamicSpawnCount = baseSpawnCount + (difficulty * extraSpawnPerDifficulty);

        Debug.Log($"<color=yellow>[Spawner]</color> 아침이 밝았습니다! (난이도: {difficulty}) -> 총 {dynamicSpawnCount}개의 폐지를 스폰합니다.");

        SpawnRandomItems(dynamicSpawnCount);
    }

    // 목표 개수를 인자로 받도록 수정된 스폰 함수
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
        int attempts = 0;
        int maxAttempts = targetSpawnCount * 3; // 무한 루프 방지용

        while (successCount < targetSpawnCount && attempts < maxAttempts)
        {
            attempts++;

            ItemDataSO data = itemDatabase[Random.Range(0, itemDatabase.Length)];

            if (spawnDict.TryGetValue(data.spawnLocation, out List<Transform> points) && points.Count > 0)
            {
                int idx = Random.Range(0, points.Count);
                Transform target = points[idx];

                GameObject obj = Instantiate(data.itemPrefab, target.position, Quaternion.identity);

                ItemBase item = obj.GetComponent<ItemBase>();
                if (item != null) item.itemData = data;

                HandleNetworkSpawn(obj);

                points.RemoveAt(idx);
                successCount++;
            }
        }
        Debug.Log($"[Spawner] {successCount}/{targetSpawnCount}개 스폰 완료. (시도 횟수: {attempts})");
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
        Debug.Log("[Spawner] 지역 관리자들과의 데이터 동기화가 완료되었습니다.");
    }

    private void HandleNetworkSpawn(GameObject obj)
    {
        if (IsServer) obj.GetComponent<NetworkObject>()?.Spawn();
    }
}