using UnityEngine;

public class DepartureButton : MonoBehaviour
{
    public SettlementZone shipZone;
    public string targetSceneName = "KJY_Lobby"; 

    // PlayerInventory에서 바라보고 E키를 누르면 호출됨
    public void Interact(PlayerInventory player)
    {
        if (shipZone != null)
        {
            //[안전장치] 정산 구역이 네트워크에 준비되었는지 확인
            if (shipZone.IsSpawned)
            {
                Debug.Log($"<color=cyan>[DepartureButton]</color> {targetSceneName}으로 복귀 및 정산 시퀀스 가동");
                shipZone.ExecuteTransition(player, targetSceneName, true);
            }
            else
            {
                Debug.LogWarning("[DepartureButton] 정산 구역(SettlementZone)이 아직 네트워크에 준비되지 않았습니다. 잠시 후 다시 시도해주세요.");
            }
        }
        else
        {
            Debug.LogError("[DepartureButton] shipZone이 연결되지 않았습니다! 인스펙터를 확인해주세요.");
        }
    }
}