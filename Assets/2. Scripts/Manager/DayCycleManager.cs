using UnityEngine;
using Unity.Netcode;

public class DayCycleManager : NetworkBehaviour
{
    [Header("Session State (서버 동기화)")]
    public NetworkVariable<int> currentDayIndex = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isSessionActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void StartNewSession()
    {
        if (!IsServer) return;
        currentDayIndex.Value = 1;
        isSessionActive.Value = true;
    }

    // [서버 전용] GameMaster가 돈 계산을 끝낸 직후 호출함
    public void ProcessDayEnd(bool isCleared)
    {
        if (!IsServer || !isSessionActive.Value) return;

        if (currentDayIndex.Value >= 5)
        {
            if (isCleared)
            {
                Debug.Log("<color=yellow>축하합니다! 주간 할당량을 달성했습니다!</color>");
                GameMaster.Instance.ClearCycle(); // 클리어 처리 지시
            }
            else
            {
                Debug.Log("<color=red>할당량 미달성... 게임 오버입니다.</color>");
                isSessionActive.Value = false;
                // TODO: 게임 오버 씬 전환
            }
        }
        else
        {
            // 1~4일차면 다음 날로 이동
            currentDayIndex.Value++;
            Debug.Log($"다음 날로 넘어갑니다. 현재 Day: {currentDayIndex.Value}");
        }
    }

    // [서버 전용] 사이클 클리어 후 GameMaster가 1일차로 리셋 지시
    public void ResetToDayOne()
    {
        if (!IsServer) return;
        currentDayIndex.Value = 1;
        Debug.Log("<color=green>1일 차로 돌아가 다음 사이클을 시작합니다.</color>");
    }
}