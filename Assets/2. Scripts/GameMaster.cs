using UnityEngine;
using Unity.Netcode;
using System;

public class GameMaster : NetworkBehaviour
{
    public static GameMaster Instance;

    [Header("Sub Managers")]
    public EconomyManager economyManager;
    public DayCycleManager dayCycleManager;

    [Header("Global Game State")]
    public NetworkVariable<int> completedCycleCount = new NetworkVariable<int>(0);

    // 이벤트 방송 (주로 UI 갱신이나 몬스터 스폰 매니저가 듣는 용도)
    public event Action<int> OnDayStarted;       // int: 현재 난이도
    public event Action<bool, int> OnDayEnded;   // bool: 전멸여부, int: 당일수익
    public event Action OnCycleCleared;          // 5일차 주간 할당량 달성 시

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
            // Destroy 대신 조용히 비활성화해야 클라이언트가 튕기지 않습니다!
            gameObject.SetActive(false);
            return;
        }
        // (GameMaster의 경우 하단에 있는 economyManager 등 GetComponent 코드는 그대로 유지)
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
            // GameSceneManager가 이미 존재한다면 이벤트를 구독합니다.
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

    // 네트워크 방이 닫히거나 연결이 끊기면 스스로 파괴 (좀비 매니저 방지)
    public override void OnNetworkDespawn()
    {

        if (Instance == this) Instance = null;

        if (IsServer && GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.OnGameSessionRequest -= StartNewGame;
        }
    }

    // --- [게임 흐름 제어 (외부에서 호출)] ---

    // 0. 게임 시작 시 호출 (서버에서 자동으로)
    public void StartNewGame()
    {
        if (!IsServer) return;
        dayCycleManager.StartNewSession();
        economyManager.ResetEconomyData();

        completedCycleCount.Value = 0;

        Debug.Log("<color=cyan>새로운 게임 세션이 시작되었습니다!</color>");
    }

    // 1. 파밍 시작 시 호출 (버튼 클릭 등)
    public void StartDay()
    {
        if (!IsServer) return;
        int difficulty = (completedCycleCount.Value * 5) + dayCycleManager.currentDayIndex.Value;

        // 시작과 관련된 구독 함수 호출
        OnDayStarted?.Invoke(difficulty);
    }

    // 2. 정산 구역(SettlementZone)에서 탈출 시 호출
    public void EndDay(bool isWipedOut, int dailyIncome, int questScore = 0)
    {
        if (!IsServer) return;

        Debug.Log(questScore);

        // [순서 보장 1] 경제 매니저에게 정산 지시 (돈부터 먼저 확실히 계산)
        economyManager.ProcessDailyIncome(isWipedOut ? 0 : dailyIncome, dayCycleManager.currentDayIndex.Value);

        // [순서 보장 2] 날짜 매니저에게 5일차인지 묻고 날짜를 넘기기
        dayCycleManager.ProcessDayEnd(economyManager.CheckWeeklyClear());

        // 구독자(경제, 날짜 매니저)들에게 방송!
        OnDayEnded?.Invoke(isWipedOut, dailyIncome);
    }

    // 3. 주간 할당량 클리어 시 (DayCycleManager가 호출)
    public void ClearCycle()
    {
        if (!IsServer) return;

        completedCycleCount.Value++;
        economyManager.PrepareNextWeek();   // 할당량 상승
        dayCycleManager.ResetToDayOne();    // 1일차로 리셋

        OnCycleCleared?.Invoke(); // UI 방송
    }

    // --- [기능] ---

    // UI(상점)에서 호출할 구매 창구

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPurchaseServerRpc(int totalPrice, int[] itemIDs, int[] counts, ulong clientId)
    {
        // 1. 서버(방장)가 직접 경제 매니저에게 돈이 충분한지 묻고 차감 시도
        if (economyManager.TryPurchaseWithLoan(totalPrice))
        {
            // 2. 돈 차감에 성공했다면, 게임 세션 매니저에게 물건 스폰 대기열 등록을 지시
            GameSessionManager.Instance.AddItemsToSpawnQueue(itemIDs, counts);

            Debug.Log($"<color=lime>[Server]</color> Client {clientId}의 {totalPrice}G 결제 승인 및 배송 등록 완료.");

            // 3. 결제를 요청한 해당 클라이언트에게만 "결제 성공했으니 장바구니 비워!" 라고 답장을 보냄
            NotifyPurchaseSuccessClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
        else
        {
            Debug.LogWarning($"<color=red>[Server]</color> Client {clientId}의 {totalPrice}G 결제 거절 (잔액 부족).");
            // 필요하다면 실패 알림을 보내는 ClientRpc를 추가할 수도 있습니다.
        }
    }

    // 특정 클라이언트(결제 요청자)에게만 보내는 성공 신호
    [Rpc(SendTo.SpecifiedInParams)]
    private void NotifyPurchaseSuccessClientRpc(RpcParams rpcParams)
    {
        ShopManager manager = FindAnyObjectByType<ShopManager>();
        manager.ClearCartUI();
    }
}