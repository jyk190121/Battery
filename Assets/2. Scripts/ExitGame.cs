using UnityEngine;
using Unity.Netcode;

public class ExitGame : MonoBehaviour
{
    /// <summary>
    /// 설정창의 exitBtn(나가기 버튼) 클릭 시 호출됩니다.
    /// </summary>
    public void OnClickExit()
    {
        if (MultiPlayerSessionManager.Instance == null) return;

        // 💡 설정창 닫기 처리
        if (SettingsUIController.Instance != null && SettingsUIController.Instance.IsSettingsOpen)
        {
            SettingsUIController.Instance.ToggleSettings();
        }

        // 💡 호스트(서버)인 경우: 
        // 현재 MultiPlayerSessionManager에 구현된 RequestDeleteSession()은 
        // 로비를 삭제하고 Shutdown을 일으키므로 모든 멤버를 타이틀로 보냅니다.
        if (NetworkManager.Singleton.IsServer)
        {
            _ = MultiPlayerSessionManager.Instance.RequestDeleteSession();
        }
        // 💡 클라이언트인 경우:
        // 본인만 Shutdown하고 LeaveAsync를 수행하여 타이틀로 돌아갑니다.
        else
        {
            MultiPlayerSessionManager.Instance.LeaveSession();
        }
    }
}