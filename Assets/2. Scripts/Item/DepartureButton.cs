using UnityEngine;

public class DepartureButton : MonoBehaviour
{
    public SettlementZone shipZone;
    public void Interact(PlayerInventory player)
    {
        if (shipZone != null)
        {
            Debug.Log("<color=red><b>[Interaction]</b> 이륙 버튼 눌림!</color>");
            shipZone.ExecuteTransition(player);
        }
    }
}