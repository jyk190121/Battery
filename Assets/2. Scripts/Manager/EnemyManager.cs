using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyManager : NetworkBehaviour
{
    public static EnemyManager Instance;

    [Header("Spawn Pool")]
    public List<MonsterData> availableMonsters;
    public List<VentController> ventPoints;

    [Header("Budget Settings")]
    public int baseMaxBudget = 10;
    public int budgetPerDifficulty = 2;

    [Header("Spawn Timing")]
    public float minSpawnDelay = 10f;
    public float maxSpawnDelay = 40f;

    private int totalMaxBudget;
    private int currentSpentBudget = 0;
    private bool isDayActive = false;
    private Coroutine spawnRoutine;

    private List<NetworkObject> activeMonsters = new List<NetworkObject>();
    private Dictionary<MonsterData, int> currentSpawnCounts = new Dictionary<MonsterData, int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (GameMaster.Instance != null)
        {
            // 1. 이벤트 구독
            GameMaster.Instance.OnDayStarted += StartSpawnCycle;
            GameMaster.Instance.OnDayEnded += StopSpawnCycle;

            // 이미 날이 시작된 상태에서 매니저가 스폰되었다면 즉시 루프 시작
            if (GameMaster.Instance.dayCycleManager != null && GameMaster.Instance.dayCycleManager.isSessionActive.Value)
            {
                int difficulty = (GameMaster.Instance.completedCycleCount.Value * 5) + GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
                StartSpawnCycle(difficulty);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (GameMaster.Instance != null)
        {
            GameMaster.Instance.OnDayStarted -= StartSpawnCycle;
            GameMaster.Instance.OnDayEnded -= StopSpawnCycle;
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    public void StartSpawnCycle(int difficulty)
    {
        if (!IsServer || isDayActive) return;

        Debug.Log($"<color=lime>[EnemyManager]</color> 스폰 사이클 시작. 난이도: {difficulty}");

        isDayActive = true;
        totalMaxBudget = baseMaxBudget + (difficulty * budgetPerDifficulty);
        currentSpentBudget = 0;

        currentSpawnCounts.Clear();
        foreach (var monster in availableMonsters)
        {
            currentSpawnCounts[monster] = 0;
        }

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        // 루프 진입 전 초기 대기 시간 부여 (게임 시작 직후 렉 방지)
        yield return new WaitForSeconds(5f);

        while (isDayActive)
        {
            float waitTime = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(waitTime);

            if (currentSpentBudget < totalMaxBudget)
            {
                TrySpawnRandomEnemy();
            }
        }
    }

    public void TrySpawnRandomEnemy()
    {
        int remainingBudget = totalMaxBudget - currentSpentBudget;

        var affordable = availableMonsters.Where(m =>
            m.spawnCost <= remainingBudget &&
            currentSpawnCounts.GetValueOrDefault(m, 0) < m.maxSpawnCount
        ).ToList();

        if (affordable.Count == 0) return;

        MonsterData selected = GetRandomMonsterByWeight(affordable);
        if (selected != null)
        {
            SpawnMonster(selected);
        }
    }

    private MonsterData GetRandomMonsterByWeight(List<MonsterData> candidates)
    {
        float totalWeight = candidates.Sum(m => m.spawnWeight);
        float randomValue = Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var monster in candidates)
        {
            currentWeight += monster.spawnWeight;
            if (randomValue <= currentWeight) return monster;
        }
        return candidates.LastOrDefault();
    }

    public void SpawnMonster(MonsterData data, bool ignoreBudget = false)
    {
        if (!IsServer || ventPoints.Count == 0) return;

        var availableVents = ventPoints.Where(v => !v.IsSpawning).ToList();
        if (availableVents.Count == 0) return;

        VentController selectedVent = availableVents[Random.Range(0, availableVents.Count)];

        // Vent에서 생성된 netObj를 RegisterActiveMonster로 받아주는 구조가 유지되어야 함
        selectedVent.TriggerSpawn(data);

        currentSpawnCounts[data] = currentSpawnCounts.GetValueOrDefault(data, 0) + 1;

        if (!ignoreBudget)
        {
            currentSpentBudget += data.spawnCost;
        }
    }

    public void RegisterActiveMonster(NetworkObject netObj)
    {
        if (netObj != null && !activeMonsters.Contains(netObj))
        {
            activeMonsters.Add(netObj);
        }
    }

    // Unregister 시 리스트에서도 확실히 제거
    public void UnregisterEnemy(MonsterData data, NetworkObject netObj = null)
    {
        if (!IsServer || data == null) return;

        currentSpentBudget = Mathf.Max(0, currentSpentBudget - data.spawnCost);

        if (currentSpawnCounts.ContainsKey(data))
        {
            currentSpawnCounts[data] = Mathf.Max(0, currentSpawnCounts[data] - 1);
        }

        if (netObj != null && activeMonsters.Contains(netObj))
        {
            activeMonsters.Remove(netObj);
        }

        Debug.Log($"[EnemyManager] {data.name} 해제됨. 남은 예산: {totalMaxBudget - currentSpentBudget}");
    }

    private void StopSpawnCycle(bool isWipedOut, int dailyIncome)
    {
        isDayActive = false;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        //  역순으로 순회하며 안전하게 데스폰
        for (int i = activeMonsters.Count - 1; i >= 0; i--)
        {
            var netObj = activeMonsters[i];
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }
        activeMonsters.Clear();
        Debug.Log("<color=red>[EnemyManager]</color> 사이클 종료 및 모든 몬스터 제거.");
    }
}