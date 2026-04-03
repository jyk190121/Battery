using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ItemSpawner : NetworkBehaviour
{
    public ItemDataSO[] itemDatabase;
    public int spawnCount = 10;

    // 💡 이제 복잡한 리스트 대신 관리자(SpawnPoint) 리스트만 들고 있으면 됩니다.
    [SerializeField] private List<ItemSpawnPoint> areaManagers = new List<ItemSpawnPoint>();

    void Start()
    {
        if (!CheckIsMultiplayer()) SpawnRandomItems();
    }

    void SpawnRandomItems()
    {
        if (itemDatabase == null || areaManagers.Count == 0) return;

        // 1. 모든 관리자로부터 좌표를 싹 모아서 지역별 사전을 만듭니다. (취합 작업)
        Dictionary<SpawnLocation, List<Transform>> spawnDict = new Dictionary<SpawnLocation, List<Transform>>();
        foreach (var manager in areaManagers)
        {
            if (!spawnDict.ContainsKey(manager.location))
                spawnDict[manager.location] = new List<Transform>();

            // 💡 관리자에게 "네가 가진 좌표들 다 줘"라고 요청 (로직 위임)
            spawnDict[manager.location].AddRange(manager.GetPoints());
        }

        // 2. 소환 로직 실행 (사용자님의 원본 랜덤 로직 유지)
        int successCount = 0;
        List<ItemDataSO> itemPool = new List<ItemDataSO>(itemDatabase);

        for (int i = 0; i < spawnCount; i++)
        {
            if (itemPool.Count == 0) break;
            ItemDataSO data = itemPool[Random.Range(0, itemPool.Count)];

            if (spawnDict.TryGetValue(data.spawnLocation, out List<Transform> points) && points.Count > 0)
            {
                int idx = Random.Range(0, points.Count);
                Transform target = points[idx];

                GameObject obj = Instantiate(data.itemPrefab, target.position, Quaternion.identity);

                // 데이터 주입 및 네트워크 처리
                ItemBase item = obj.GetComponent<ItemBase>();
                if (item != null) item.itemData = data;
                HandleNetworkSpawn(obj);

                points.RemoveAt(idx); // 중복 방지
                successCount++;
            }
        }
        Debug.Log($"[Spawner] {successCount}개 아이템 스폰 완료.");
    }

    [ContextMenu("Bake: 모든 지역 관리자 동기화")]
    public void RefreshSpawnPoints()
    {
        areaManagers.Clear();
        // 1. 씬에서 모든 지역 관리자를 찾습니다.
        ItemSpawnPoint[] found = Object.FindObjectsByType<ItemSpawnPoint>(FindObjectsSortMode.None);

        foreach (var manager in found)
        {
            // 2. 💡 각 관리자에게 자기 자식들을 스스로 갱신하라고 시킵니다.
            manager.UpdateChildPoints();
            areaManagers.Add(manager);
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[Spawner] 지역 관리자들과의 데이터 동기화가 완료되었습니다.");
    }

    // --- 하단 네트워크 로직은 이전과 동일하게 유지 ---
    private bool CheckIsMultiplayer() => NetworkGlobalSettings.Instance != null && NetworkGlobalSettings.Instance.isMultiplayerMode;

    private void HandleNetworkSpawn(GameObject obj)
    {
        if (CheckIsMultiplayer() && IsServer) obj.GetComponent<NetworkObject>()?.Spawn();
    }
}