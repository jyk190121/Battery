using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyManager : NetworkBehaviour
{
    public static EnemyManager Instance;

    [Header("Spawn Pool")]
    [Tooltip("이번 맵에 등장할 수 있는 몬스터들의 데이터 리스트 (비용, 가중치 포함)")]
    public List<MonsterData> availableMonsters;
    [Tooltip("맵 곳곳에 배치된 환풍구(스폰 지점) 리스트")]
    public List<VentController> ventPoints;

    [Header("Budget Settings")]
    [Tooltip("스테이지 시작 시 기본 스폰 예산 (점수)")]
    public int baseMaxBudget = 10;
    [Tooltip("난이도가 1 오를 때마다 추가되는 예산")]
    public int budgetPerDifficulty = 2;

    [Header("Spawn Timing")]
    [Tooltip("스폰을 시도하는 최소 대기 시간")]
    public float minSpawnDelay = 10f;
    [Tooltip("스폰을 시도하는 최대 대기 시간 (최소~최대 사이 랜덤으로 스폰 시도)")]
    public float maxSpawnDelay = 40f;

    private int totalMaxBudget;             // 오늘 쓸 수 있는 총 예산 한도
    private int currentSpentBudget = 0;     // 현재 맵에 돌아다니는 몹들의 비용 합계
    private bool isDayActive = false;       // 현재 파밍(스폰)이 진행 중인지 여부
    private Coroutine spawnRoutine;         // 스폰 루프 코루틴

    // 퇴근(DayEnded) 시 맵에 남은 몬스터들을 일괄 삭제하기 위한 명부
    private List<NetworkObject> activeMonsters = new List<NetworkObject>();

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
        }
    }

    public void StartSpawnCycle(int difficulty)
    {
        isDayActive = true;
        // 오늘의 난이도에 맞춰 총 예산 책정 (예: 기본 10 + 난이도 5 * 2 = 20점)
        totalMaxBudget = baseMaxBudget + (difficulty * budgetPerDifficulty);
        currentSpentBudget = 0;// 아직 한 마리도 안 뽑았으므로 0

        // 게임이 시작될 때 전역(2D)으로 스산한 시스템 경고음을 틀어줍니다
        // 서버에서 알림을 울리면 클라이언트 동기화를 위해 RPC를 사용하거나, 
        // GameMaster의 OnDayStarted 이벤트에 클라이언트의 SoundManager가 직접 리스닝하는 것도 좋습니다.
        //PlayGlobalWarningClientRpc();

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    //[ClientRpc]
    //private void PlayGlobalWarningClientRpc()
    //{
    //    // 2D 사운드이므로 기존의 PlaySfx를 바로 사용합니다.
    //    SoundManager.Instance.PlaySfx(SfxSound.SYSTEM_WARNING);
    //}

    private IEnumerator SpawnRoutine()
    {
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
        Debug.Log("몹 소환");

        int remainingBudget = totalMaxBudget - currentSpentBudget;

        var affordable = availableMonsters.Where(m => m.spawnCost <= remainingBudget).ToList();
        if (affordable.Count == 0) return;

        // 가중치 랜덤 
        // 현재는 임시로 무조건 리스트의 0번째 몬스터를 뽑습니다.
        // 추후 이 부분을 'GetRandomMonsterByWeight()' 같은 가중치 랜덤 함수로 교체해야 완벽
        MonsterData selected = affordable[0]; // 임시

        if (selected != null)
        {
            SpawnMonster(selected);
        }
    }

    public void SpawnMonster(MonsterData data, bool ignoreBudget = false)
    {
        if (!IsServer) return;

        Debug.Log("몹 소환");

        if (ventPoints.Count == 0) return;

        var availableVents = ventPoints.Where(v => !v.IsSpawning).ToList();
        if (availableVents.Count == 0) return;

        VentController selectedVent = availableVents[Random.Range(0, availableVents.Count)];
        selectedVent.TriggerSpawn(data);

        if (!ignoreBudget)
        {
            currentSpentBudget += data.spawnCost;
            Debug.Log($"[EnemyManager] {data.name} 정규 스폰됨. (현재 점수: {currentSpentBudget}/{totalMaxBudget})");
        }
        else
        {
            Debug.Log($"<color=orange>[EnemyManager]</color> {data.name} 강제(이벤트) 스폰됨! (예산 미소모)");
        }
    }

    public void RegisterActiveMonster(NetworkObject netObj)
    {
        if (netObj != null && !activeMonsters.Contains(netObj))
        {
            activeMonsters.Add(netObj);
        }
    }

    public void UnregisterEnemy(int cost)
    {
        if (!IsServer) return;
        currentSpentBudget = Mathf.Max(0, currentSpentBudget - cost);
    }

    private void StopSpawnCycle(bool isWipedOut, int dailyIncome)
    {
        isDayActive = false;
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);

        foreach (var netObj in activeMonsters)
        {
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        }
        activeMonsters.Clear();
    }
}