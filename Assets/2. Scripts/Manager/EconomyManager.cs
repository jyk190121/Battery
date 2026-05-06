// 순수 '보유 재화' 관리 기능
using UnityEngine;
using Unity.Netcode;

public class EconomyManager : NetworkBehaviour
{
    [Header("Synced Economy Data")]
    public NetworkVariable<int> currentTotalGold = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer) ResetEconomyData();
    }

    // [서버 전용] GameMaster가 직접 호출
    public void ProcessDailyIncome(int totalDailyIncome, int currentDayIndex)
    {
        if (!IsServer) return;

        currentTotalGold.Value += totalDailyIncome;
        Debug.Log($"<color=green>Day {currentDayIndex} 정산: 오늘 수익 {totalDailyIncome}G / 총 보유 자산: {currentTotalGold.Value}G</color>");
    }

    // 상점 결제 시 호출되는 차감 함수
    public bool TryPurchase(int price)
    {
        if (!IsServer) return false;

        if (currentTotalGold.Value >= price)
        {
            currentTotalGold.Value -= price;
            return true;
        }
        return false;
    }

    public void ResetEconomyData()
    {
        if (!IsServer) return;
        currentTotalGold.Value = 0;
    }
}