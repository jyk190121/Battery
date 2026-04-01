using UnityEngine;

public class MissionStartButton : MonoBehaviour
{
    [Header("Settings")]
    public SettlementZone shipZone;        // 정비 씬에 배치된 SettlementZone 연결
    public string gameSceneName = "GameScene"; // 이동할 게임 씬 이름

    public void Interact(PlayerInventory player)
    {
        if (shipZone != null)
        {
            Debug.Log("<color=green><b>[Mission]</b> 장비를 챙겨 게임 씬으로 출발합니다!</color>");

            // SettlementZone의 목적지를 게임 씬으로 명시적 설정
            shipZone.nextSceneName = gameSceneName;

            // 공간 스캔 -> 데이터 저장 -> 씬 이동 실행
            shipZone.ExecuteTransition(player);
        }
        else
        {
            Debug.LogError("🚨 MissionStartButton에 SettlementZone이 연결되지 않았습니다!");
        }
    }
}