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
    [Tooltip("스폰 가능한 몬스터 데이터 목록")]
    public List<MonsterData> availableMonsters;
    [Tooltip("맵에 배치된 몬스터 스폰 지점(환풍구 등)")]
    public List<VentController> ventPoints;

    [Header("--- Special Spawn (고스트) ---")]
    [Tooltip("영적 세계 기믹용 고스트 데이터")]
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
            // 1. 낮/밤(사이클) 시작 및 종료 이벤트 구독
            GameMaster.Instance.OnDayStarted += StartSpawnCycle;
            GameMaster.Instance.OnDayEnded += StopSpawnCycle;

            // 2. 이미 날이 시작된 상태에서 매니저가 스폰되었다면 즉시 루프 시작 (난입/재접속 대비)
            if (GameMaster.Instance.dayCycleManager != null && GameMaster.Instance.dayCycleManager.isSessionActive.Value)
            {
                int difficulty = (GameMaster.Instance.completedCycleCount.Value * 5) + GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
                StartSpawnCycle(difficulty);
            }
        }
        //QuestManager.OnSpiritualWorldEntered += HandleGhostSpawn;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (GameMaster.Instance != null)
        {
            GameMaster.Instance.OnDayStarted -= StartSpawnCycle;
            GameMaster.Instance.OnDayEnded -= StopSpawnCycle;
        }

        //QuestManager.OnSpiritualWorldEntered -= HandleGhostSpawn;

        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }


    // =========================================================
    // 3. 유니티 루프 
    // =========================================================

    // 본 스크립트에서는 미사용 (스폰은 코루틴으로 대체하여 성능 최적화)


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    /// <summary>
    /// [서버 전용] 하루가 시작될 때 예산을 책정하고 스폰 코루틴을 돌립니다.
    /// </summary>
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

        // 예산이 충분하고, 최대 스폰 마리 수를 넘지 않은 몬스터만 후보군으로 추림
        var affordable = availableMonsters.Where(m =>
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

    /// <summary>
    /// [서버 전용] 특정 몬스터를 사용 가능한 벤트를 통해 스폰시킵니다.
    /// </summary>
    public void SpawnMonster(MonsterData data, bool ignoreBudget = false)
    {
        if (!IsServer || ventPoints.Count == 0) return;

        // 현재 스폰 연출을 하고 있지 않은 빈 벤트들만 색출
        var availableVents = ventPoints.Where(v => !v.IsSpawning).ToList();
        if (availableVents.Count == 0) return;

        VentController selectedVent = availableVents[Random.Range(0, availableVents.Count)];

        // 벤트에서 생성된 NetObj를 RegisterActiveMonster로 받아주는 구조
        selectedVent.TriggerSpawn(data);

        _currentSpawnCounts[data] = _currentSpawnCounts.GetValueOrDefault(data, 0) + 1;

        if (!ignoreBudget)
        {
            _currentSpentBudget += data.spawnCost;
        }
    }

    /// <summary>
    /// 몬스터 본체(NetworkObject)를 활성화 리스트에 등록합니다.
    /// </summary>
    public void RegisterActiveMonster(NetworkObject netObj)
    {
        if (netObj != null && !_activeMonsters.Contains(netObj))
        {
            _activeMonsters.Add(netObj);
        }
    }

    /// <summary>
    /// 몬스터가 죽거나 디스폰될 때 예산을 반환하고 리스트에서 제거합니다.
    /// </summary>
    public void UnregisterEnemy(MonsterData data, NetworkObject netObj = null)
    {
        if (!IsServer || data == null) return;

        // 예산 및 마리수 롤백 (안전하게 0 이하로 떨어지지 않도록 Max 처리)
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

    // ---------------------------------------------------------
    // [사운드 최적화 전용] EnvironmentScanner 관리 창구
    // ---------------------------------------------------------
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

    /// <summary>
    /// 지정된 딜레이마다 스폰을 시도하는 핵심 루프 코루틴입니다.
    /// </summary>
    private IEnumerator SpawnRoutine()
    {
        // 루프 진입 전 초기 대기 시간 부여 (게임 시작 직후 렉 및 급사 방지)
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

    /// <summary>
    /// 가중치(Weight) 기반 확률 뽑기로 스폰할 몬스터를 결정합니다.
    /// </summary>
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

        // 오차 대비 안전장치
        return candidates.LastOrDefault();
    }

    /// <summary>
    /// 하루가 끝나면 모든 몬스터를 창고(풀)로 돌려보내고 스폰을 정지합니다.
    /// </summary>
    private void StopSpawnCycle(bool isWipedOut, int dailyIncome)
    {
        _isDayActive = false;
        _isGhostSpawned = false;

        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        // 리스트에서 요소를 삭제할 때는 역순(for-loop 역방향)으로 순회해야 에러가 나지 않습니다.
        for (int i = _activeMonsters.Count - 1; i >= 0; i--)
        {
            var netObj = _activeMonsters[i];
            if (netObj != null && netObj.IsSpawned)
            {
                if (netObj.TryGetComponent<MonsterController>(out var controller))
                {
                    // Despawn(true)로 파괴하지 않고 Object Pool로 안전하게 반환
                    MonsterPool.Instance.ReturnMonster(controller.monsterData.monsterPrefab, netObj);
                }
            }
        }

        _activeMonsters.Clear();
        Debug.Log("<color=red>[EnemyManager]</color> 사이클 종료! 모든 몬스터를 수거했습니다.");
    }

    /// <summary>
    /// 이벤트 발생 시 영적 세계 벤트에서 귀신을 스폰합니다.
    /// </summary>
    private void HandleGhostSpawn()
    {
        if (!IsServer || _isGhostSpawned || ghostVent == null) return;

        // availableMonsters 리스트 안에서 Ghost 타입인 데이터를 찾습니다.
        MonsterData ghostData = availableMonsters.Find(m => m.type == MonsterType.Ghost);

        if (ghostData != null)
        {
            // 귀신 전용 벤트에서 스폰 (예산 무시 옵션인 ignoreBudget=true 적용 가능)
            ghostVent.TriggerSpawn(ghostData);
            _isGhostSpawned = true; 

            Debug.Log("<color=magenta>[EnemyManager]</color> 영적 세계 진입 감지 전용 벤트에서 Ghost를 확정 스폰합니다.");
        }
        else
        {
            Debug.LogWarning("[EnemyManager] availableMonsters 리스트에 Ghost 타입의 몬스터 데이터가 없습니다!");
        }
    }
}