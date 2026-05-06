// 실적 점수(생존 점수) 관리
using UnityEngine;
using Unity.Netcode;

public class PerformanceManager : NetworkBehaviour
{
    [Header("Performance Settings")]
    public int baseTargetScore = 20;        // 1주차 기본 목표 점수
    public int baseGrowthAmount = 20;       // 상승폭 기준값
    public float curveMultiplier = 1.5f;    // 상승 곡선 가중치
    public int maxTargetScore = 920;        // 최대 상한선 

    [Header("Synced Performance Data")]
    public NetworkVariable<int> dynamicTargetScore = new NetworkVariable<int>(20, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> accumulatedScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer) ResetPerformanceData();
    }

    // 하루 일과가 끝나고 퀘스트 점수를 정산받을 때
    public void ProcessDailyScore(int questScore)
    {
        if (!IsServer) return;

        accumulatedScore.Value += questScore;
        Debug.Log($"<color=cyan>[Performance] 오늘 획득 실적: {questScore} / 이번 주 누적: {accumulatedScore.Value} (목표: {dynamicTargetScore.Value})</color>");
    }

    // 주간 생존 여부 판정 (5일차에 호출)
    public bool CheckWeeklyClear()
    {
        return accumulatedScore.Value >= dynamicTargetScore.Value;
    }

    // 주간 사이클 클리어 시 난이도 상승 처리
    public void PrepareNextWeek(int completedCycles)
    {
        if (!IsServer) return;

        // 곡선형 목표 점수 계산: 기본점수 + (기본상승폭 * (완료한사이클수 ^ 곡선가중치))
        int calculatedTarget = baseTargetScore + Mathf.FloorToInt(baseGrowthAmount * Mathf.Pow(completedCycles, curveMultiplier));

        // 최대 상한선(920)을 초과하지 않도록 억제
        dynamicTargetScore.Value = Mathf.Min(calculatedTarget, maxTargetScore);

        // 다음 주차 점수 수집을 위해 누적 점수 초기화
        accumulatedScore.Value = 0;

        Debug.Log($"<color=magenta>[Performance] 다음 주차 목표 실적 갱신: {dynamicTargetScore.Value}점 (상한선 {maxTargetScore}점)</color>");
    }

    // 게임 오버 후 재시작 시 초기화
    public void ResetPerformanceData()
    {
        if (!IsServer) return;
        dynamicTargetScore.Value = baseTargetScore;
        accumulatedScore.Value = 0;
    }
}