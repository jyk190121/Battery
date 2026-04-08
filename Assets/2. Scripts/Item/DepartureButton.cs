using UnityEngine;
public class DepartureButton : MonoBehaviour
{
    public SettlementZone shipZone;
    public string targetSceneName = "LobbyScene";

    public void Interact(PlayerInventory player)
    {
        if (shipZone != null)
        {
            Debug.Log("<color=red><b>[Interaction]</b> 빚을 갚으러 복귀합니다! (정산 O)</color>");
            // 💡 [보완] 정산 진행 (true)
            shipZone.ExecuteTransition(player, targetSceneName, true);
        }
    }
}