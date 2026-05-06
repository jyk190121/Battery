using UnityEngine;
using Unity.Netcode;
using System;

public class GameMaster : NetworkBehaviour
{
    public static GameMaster Instance;

    [Header("Sub Managers")]
    public EconomyManager economyManager;
    public DayCycleManager dayCycleManager;
    public PerformanceManager performanceManager;

    [Header("Global Game State")]
    public NetworkVariable<int> completedCycleCount = new NetworkVariable<int>(0);

    public event Action<int> OnDayStarted;
    public event Action<bool, int> OnDayEnded;
    public event Action OnCycleCleared;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            gameObject.SetActive(false);
            return;
        }
    }

    public static void SpawnManager(GameObject prefab)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (Instance != null)
        {
            NetworkObject oldNetObj = Instance.GetComponent<NetworkObject>();
            if (oldNetObj != null && !oldNetObj.IsSpawned)
            {
                Destroy(Instance.gameObject);
                Instance = null;
            }
            else return;
        }

        GameObject go = Instantiate(prefab);
        NetworkObject netObj = go.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.Spawn();
            Debug.Log($"<color=lime>[GameMaster]</color> 프리팹 기반 런타임 스폰 완료.");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (GameSceneManager.Instance != null)
            {
                GameSceneManager.Instance.OnGameSessionRequest += StartNewGame;

                if (GameSceneManager.Instance.IsSessionInitialized)
                {
                    StartNewGame();
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;

        if (IsServer && GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.OnGameSessionRequest -= StartNewGame;
        }
    }

    public void StartNewGame()
    {
        if (!IsServer) return;
        dayCycleManager.StartNewSession();
        economyManager.ResetEconomyData();
        performanceManager.ResetPerformanceData();

        completedCycleCount.Value = 0;

        Debug.Log("<color=cyan>새로운 게임 세션이 시작되었습니다!</color>");
    }

    public void StartDay()
    {
        if (!IsServer) return;

        // 1주일이 4일이므로 난이도 배수를 5에서 4로 변경
        int difficulty = (completedCycleCount.Value * 4) + dayCycleManager.currentDayIndex.Value;
        OnDayStarted?.Invoke(difficulty);
    }

    public void EndDay(bool isWipedOut, int dailyIncome, int questScore = 0)
    {
        if (!IsServer) return;

        // [순서 보장 1] 돈 정산 (지갑 기능)
        economyManager.ProcessDailyIncome(isWipedOut ? 0 : dailyIncome, dayCycleManager.currentDayIndex.Value);

        // [순서 보장 2] 실적 점수 정산 (생존 기능)
        performanceManager.ProcessDailyScore(isWipedOut ? 0 : questScore);

        // [순서 보장 3] 실적 점수를 기준으로 주간 생존 여부 판정
        dayCycleManager.ProcessDayEnd(performanceManager.CheckWeeklyClear());

        OnDayEnded?.Invoke(isWipedOut, dailyIncome);
    }

    public void ClearCycle()
    {
        if (!IsServer) return;

        completedCycleCount.Value++;
        performanceManager.PrepareNextWeek(completedCycleCount.Value);
        dayCycleManager.ResetToDayOne();

        OnCycleCleared?.Invoke();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPurchaseServerRpc(int totalPrice, int[] itemIDs, int[] counts, ulong clientId)
    {
        if (economyManager.TryPurchase(totalPrice))
        {
            GameSessionManager.Instance.AddItemsToSpawnQueue(itemIDs, counts);
            Debug.Log($"<color=lime>[Server]</color> Client {clientId}의 {totalPrice}G 결제 승인 및 배송 등록 완료.");
            NotifyPurchaseSuccessClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
        else
        {
            Debug.LogWarning($"<color=red>[Server]</color> Client {clientId}의 {totalPrice}G 결제 거절 (잔액 부족).");
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void NotifyPurchaseSuccessClientRpc(RpcParams rpcParams)
    {
        ShopManager manager = FindAnyObjectByType<ShopManager>();
        manager.ClearCartUI();
    }
}