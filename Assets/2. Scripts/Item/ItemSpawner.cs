using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ItemSpawner : NetworkBehaviour
{
    public ItemDataSO[] itemDatabase;
    public int spawnCount = 10;

    [SerializeField] private List<ItemSpawnPoint> areaManagers = new List<ItemSpawnPoint>();

    // 🗑️ Start() 함수와 CheckIsMultiplayer() 함수 완전 삭제 완료

    void SpawnRandomItems()
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
        int maxAttempts = spawnCount * 3; // 무한 루프 방지용

        while (successCount < spawnCount && attempts < maxAttempts)
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
        Debug.Log($"[Spawner] {successCount}개 아이템 스폰 완료. (총 시도 횟수: {attempts})");
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
        // 💡 [수정됨] 싱글/멀티 구분을 없애고, 무조건 서버(Host) 권한으로만 스폰
        if (IsServer) obj.GetComponent<NetworkObject>()?.Spawn();
    }

    public override void OnNetworkSpawn()
    {
        // 💡 [수정됨] 방장이 방을 생성했을 때 1회 스폰
        if (IsServer)
        {
            SpawnRandomItems();
        }
    }
}