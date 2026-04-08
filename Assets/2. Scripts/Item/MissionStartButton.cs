using UnityEngine;
public class MissionStartButton : MonoBehaviour
{
    public SettlementZone shipZone;
    public string targetSceneName = "GameScene";

    public void Interact(PlayerInventory player)
    {
        if (shipZone != null)
        {
            Debug.Log("<color=green><b>[Mission]</b> 장비를 챙겨 출발합니다! (정산 X)</color>");
            // 💡 [보완] 정산 안 함 (false)
            shipZone.ExecuteTransition(player, targetSceneName, false);
        }
    }
}