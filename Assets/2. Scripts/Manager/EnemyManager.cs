using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 게임 내 몬스터의 스폰 예산(Budget)을 관리하고, 
/// 벤트를 통해 몬스터를 생성 및 회수하며 서버의 과부하를 막는 AI 디렉터입니다.
/// </summary>
public class EnemyManager : NetworkBehaviour
{
    // =========================================================
    // 1. 변수 선언부
    // =========================================================

    public static EnemyManager Instance;

    [Header("--- Spawn Pool ---")]
    [Tooltip("스폰 가능한 몬스터 데이터 목록 (여기에 고스트가 들어가도 랜덤 스폰에서 자동 제외됩니다)")]
    public List<MonsterData> availableMonsters;
    [Tooltip("맵에 배치된 일반 몬스터 스폰 지점(학교 환풍구 등)")]
    public List<VentController> ventPoints;

    [Header("--- Special Spawn (고스트) ---")]
    [Tooltip("영적 세계 기믹용 고스트 데이터 (직접 할당)")]
    public MonsterData ghostData;
    [Tooltip("영적 세계 고스트 전용 스폰 환풍구")]
    public VentController ghostVent;

    [Header("--- Budget Settings ---")]
    [Tooltip("기본 스폰 예산")]
    public int baseMaxBudget = 10;
    [Tooltip("난이도 1당 추가되는 예산")]
    public int budgetPerDifficulty = 2;

    [Header("--- Spawn Timing ---")]
    public float minSpawnDelay = 10f;
    public float maxSpawnDelay = 40f;

    // [서버 최적화용] 활성화된 몬스터들의 청각/시각 스캐너 리스트 
    private List<EnvironmentScanner> _activeScanners = new List<EnvironmentScanner>();
    public List<EnvironmentScanner> ActiveScanners => _activeScanners;

    private int _totalMaxBudget;
    private int _currentSpentBudget = 0;
    private bool _isDayActive = false;
    private Coroutine _spawnRoutine;
    private bool _isGhostSpawned = false;

    private List<NetworkObject> _activeMonsters = new List<NetworkObject>();
    private Dictionary<MonsterData, int> _currentSpawnCounts = new Dictionary<MonsterData, int>();


    // =========================================================
    // 2. 초기화 함수
    // =========================================================

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
            GameMaster.Instance.OnDayStarted += StartSpawnCycle;
            GameMaster.Instance.OnDayEnded += StopSpawnCycle;

            if (GameMaster.Instance.dayCycleManager != null && GameMaster.Instance.dayCycleManager.isSessionActive.Value)
            {
                int difficulty = (GameMaster.Instance.completedCycleCount.Value * 5) + GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
                StartSpawnCycle(difficulty);
            }
        }

        QuestReturnPoint.OnSpiritualWorldEntered += HandleGhostSpawn;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (GameMaster.Instance != null)
        {
            GameMaster.Instance.OnDayStarted -= StartSpawnCycle;
            GameMaster.Instance.OnDayEnded -= StopSpawnCycle;
        }

        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        QuestReturnPoint.OnSpiritualWorldEntered -= HandleGhostSpawn;
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    public void StartSpawnCycle(int difficulty)
    {
        if (!IsServer || _isDayActive) return;

        Debug.Log($"<color=lime>[EnemyManager]</color> 스폰 사이클 시작. 적용 난이도: {difficulty}");

        _isDayActive = true;
        _totalMaxBudget = baseMaxBudget + (difficulty * budgetPerDifficulty);
        _currentSpentBudget = 0;

        _currentSpawnCounts.Clear();
        foreach (var monster in availableMonsters)
        {
            _currentSpawnCounts[monster] = 0;
        }

        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
        _spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    /// <summary>
    /// [서버 전용] 예산이 허락하는 한도 내에서 랜덤한 몬스터 스폰을 시도합니다.
    /// </summary>
    public void TrySpawnRandomEnemy()
    {
        int remainingBudget = _totalMaxBudget - _currentSpentBudget;

        var affordable = availableMonsters.Where(m =>
            m.type != MonsterType.Ghost &&
            m.spawnCost <= remainingBudget &&
            _currentSpawnCounts.GetValueOrDefault(m, 0) < m.maxSpawnCount
        ).ToList();

        if (affordable.Count == 0) return;

        MonsterData selected = GetRandomMonsterByWeight(affordable);
        if (selected != null)
        {
            SpawnMonster(selected);
        }
    }

    public void SpawnMonster(MonsterData data, bool ignoreBudget = false)
    {
        if (!IsServer || ventPoints.Count == 0) return;

        var availableVents = ventPoints.Where(v => !v.IsSpawning).ToList();
        if (availableVents.Count == 0) return;

        VentController selectedVent = availableVents[Random.Range(0, availableVents.Count)];

        selectedVent.TriggerSpawn(data);

        _currentSpawnCounts[data] = _currentSpawnCounts.GetValueOrDefault(data, 0) + 1;

        if (!ignoreBudget)
        {
            _currentSpentBudget += data.spawnCost;
        }
    }

    public void RegisterActiveMonster(NetworkObject netObj)
    {
        if (netObj != null && !_activeMonsters.Contains(netObj))
        {
            _activeMonsters.Add(netObj);
        }
    }

    public void UnregisterEnemy(MonsterData data, NetworkObject netObj = null)
    {
        if (!IsServer || data == null) return;

        _currentSpentBudget = Mathf.Max(0, _currentSpentBudget - data.spawnCost);

        if (_currentSpawnCounts.ContainsKey(data))
        {
            _currentSpawnCounts[data] = Mathf.Max(0, _currentSpawnCounts[data] - 1);
        }

        if (netObj != null && _activeMonsters.Contains(netObj))
        {
            _activeMonsters.Remove(netObj);
        }

        Debug.Log($"<color=orange>[EnemyManager]</color> {data.name} 해제됨. 남은 스폰 예산: {_totalMaxBudget - _currentSpentBudget}");
    }

    public void RegisterScanner(EnvironmentScanner scanner)
    {
        if (!_activeScanners.Contains(scanner)) _activeScanners.Add(scanner);
    }

    public void UnregisterScanner(EnvironmentScanner scanner)
    {
        if (_activeScanners.Contains(scanner)) _activeScanners.Remove(scanner);
    }


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(5f);

        while (_isDayActive)
        {
            float waitTime = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(waitTime);

            if (_currentSpentBudget < _totalMaxBudget)
            {
                TrySpawnRandomEnemy();
            }
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

    private void StopSpawnCycle(bool isWipedOut, int dailyIncome)
    {
        _isDayActive = false;
        _isGhostSpawned = false;

        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        for (int i = _activeMonsters.Count - 1; i >= 0; i--)
        {
            var netObj = _activeMonsters[i];
            if (netObj != null && netObj.IsSpawned)
            {
                if (netObj.TryGetComponent<MonsterController>(out var controller))
                {
                    MonsterPool.Instance.ReturnMonster(controller.monsterData.monsterPrefab, netObj);
                }
            }
        }

        _activeMonsters.Clear();
        Debug.Log("<color=red>[EnemyManager]</color> 사이클 종료! 모든 몬스터를 수거했습니다.");
    }

    /// <summary>
    /// 고스트 소환
    /// </summary>
    private void HandleGhostSpawn()
    {
        if (!IsServer || _isGhostSpawned || ghostVent == null) return;

        if (ghostData != null)
        {
            // 귀신 전용 벤트에서 예산 무시 옵션으로 스폰!
            ghostVent.TriggerSpawn(ghostData);
            _isGhostSpawned = true;

            Debug.Log("<color=magenta>[EnemyManager]</color> 영적 세계 진입 감지! 전용 벤트에서 Ghost를 다이렉트로 스폰합니다.");
        }
        else
        {
            Debug.LogError("[EnemyManager] 인스펙터의 ghostData 슬롯이 비어있습니다! 귀신 SO 데이터를 드래그해서 넣어주세요.");
        }
    }
}