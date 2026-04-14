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
        if (Instance == null) { Instance = this; }
        else Destroy(gameObject);

        if(economyManager == null) economyManager = GetComponent<EconomyManager>();
        if(dayCycleManager == null) dayCycleManager = GetComponent<DayCycleManager>();
    }

    public override void OnNetworkSpawn()
    {
        if(IsServer)
        {
            StartNewGame();
        }
    }

    // 네트워크 방이 닫히거나 연결이 끊기면 스스로 파괴 (좀비 매니저 방지)
    public override void OnNetworkDespawn()
    {
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    // --- [게임 흐름 제어 (외부에서 호출)] ---

    // 0. 게임 시작 시 호출 (서버에서 자동으로)
    public void StartNewGame()
    {
        if (!IsServer) return;
        dayCycleManager.StartNewSession();
        economyManager.ResetEconomyData();

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
    public void EndDay(bool isWipedOut, int dailyIncome)
    {
        if (!IsServer) return;

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
    public bool RequestPurchase(int price)
    {
        if (!IsServer) return false;
        // 경제 매니저에게 묻고 결과를 그대로 돌려줌
        return economyManager.TryPurchaseWithLoan(price);
    }
}