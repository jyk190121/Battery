using UnityEngine;
using Unity.Netcode;

public class DepartureButton : NetworkBehaviour
{
    public SettlementZone shipZone;
    public string targetSceneName = "LobbyScene";

    public void Interact(PlayerInventory player)
    {
       

        // 1. 구역에 이동 요청 (정산 O, 씬 이름 전달)
        if (shipZone != null)
        {
            shipZone.ExecuteTransition(player, targetSceneName, true);
        }
    }
}