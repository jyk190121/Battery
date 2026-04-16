using System.Globalization;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameSync : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> NetworkNickname = new NetworkVariable<FixedString64Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            NetworkNickname.Value = MultiPlayerSessionManager.Instance.PlayerNickname;
            
            if(GlobalVoiceManager.Instance != null)
            {
                string safeNick = NetworkNickname.Value.ToString().Replace("\0", "").Trim(); 
                GlobalVoiceManager.Instance.ConnectVoice(safeNick);
                Debug.Log($"[PlayerNameSync] 플레이어 '{safeNick}'의 닉네임으로 보이스 채팅 서버에 연결 요청을 보냈습니다.");
            }

        }

        // 변수가 바뀌면 UI 매니저에게 갱신 요청
        NetworkNickname.OnValueChanged += (oldV, newV) => {
            if (MultiplayerUIManager.Instance != null) MultiplayerUIManager.Instance.RefreshPlayerList();
        };

        // 입장 시 즉시 갱신
        if (MultiplayerUIManager.Instance != null) MultiplayerUIManager.Instance.RefreshPlayerList();

        // 씬이 로드되거나 스폰될 때 실행
        ApplyNicknameToUI();

        // 네트워크 변수가 바뀔 때도 실행 (다른 사람이 들어왔을 때)
        NetworkNickname.OnValueChanged += (oldV, newV) => ApplyNicknameToUI();
    }
    void ApplyNicknameToUI()
    {
        // 현재 씬에 MultiplayerUIManager가 있는지 확인
        if (MultiplayerUIManager.Instance != null)
        {
            // OwnerClientId를 슬롯 인덱스로 활용 (입장 순서대로 0, 1, 2, 3...)
            int slotIndex = (int)OwnerClientId;
            string name = NetworkNickname.Value.ToString();

            MultiplayerUIManager.Instance.UpdatePlayerName(slotIndex, name, IsOwner);
        }
    }
}