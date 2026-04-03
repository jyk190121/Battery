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

        Dictionary<SpawnLocation, List<Transform>> spawnDict = new Dictionary<SpawnLocation, List<Transform>>();
        foreach (var manager in areaManagers)
        {
            if (!spawnDict.ContainsKey(manager.location))
                spawnDict[manager.location] = new List<Transform>();

            spawnDict[manager.location].AddRange(manager.GetPoints());
        }

        int successCount = 0;
        int attempts = 0;
        int maxAttempts = spawnCount * 3; // 무한 루프 방지용 (원하는 개수의 3배수까지만 시도)

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

    public override void OnNetworkSpawn()
    {
        // 멀티플레이 모드일 때, 방장(서버) 권한으로만 아이템을 한 번 뿌립니다.
        if (CheckIsMultiplayer() && IsServer)
        {
            SpawnRandomItems();
        }
    }
}