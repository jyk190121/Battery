using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class StartButton : MonoBehaviour
{
    [Header("설정")]
    public string targetSceneName = "KJY_Player";

    /// <summary>
    /// UI Button의 OnClick 이벤트에서 이 함수를 호출하세요.
    /// </summary>
    public void OnClickStart()
    {
        // 1. 뇌(GameSessionManager)가 씬에 존재하는지 확인
        if (GameSessionManager.Instance == null)
        {
            Debug.LogError("[StartButton] GameSessionManager 인스턴스를 찾을 수 없습니다!");
            return;
        }

        // 2. 💡 [핵심] 뇌가 네트워크에 연결(Spawn)된 상태인지 확인
        if (GameSessionManager.Instance.IsSpawned)
        {
            Debug.Log("<color=cyan>[StartButton]</color> 매니저에게 게임 시작 요청을 보냅니다.");

            // 뇌가 가지고 있는 RPC를 호출합니다. (누가 누르든 서버로 신호가 갑니다)
            GameSessionManager.Instance.RequestStartGameServerRpc(targetSceneName);
        }
        else
        {
            Debug.LogWarning("<color=orange>[StartButton]</color> 네트워크가 아직 준비되지 않았습니다. 잠시 후 다시 시도하세요.");
        }
    }
}