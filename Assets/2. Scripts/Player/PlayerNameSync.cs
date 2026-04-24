using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameSync : NetworkBehaviour
{
    public static event System.Action<string, Transform> OnNicknameSynced;

    public static event System.Action OnplayerRosterChanged;

    public NetworkVariable<FixedString64Bytes> NetworkNickname = new NetworkVariable<FixedString64Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {

            string localNick = MultiPlayerSessionManager.Instance.PlayerNickname;
            SetNicknameServerRpc(localNick);

            //NetworkNickname.OnValueChanged += OnNicknameChanged;

            if (!string.IsNullOrEmpty(NetworkNickname.Value.ToString()))
            {
                ApplyNicknameToUI(NetworkNickname.Value.ToString());
            }

            //NetworkNickname.Value = MultiPlayerSessionManager.Instance.PlayerNickname;
            
            //if(GlobalVoiceManager.Instance != null)
            //{
            //    string safeNick = NetworkNickname.Value.ToString().Replace("\0", "").Trim(); 
            //    GlobalVoiceManager.Instance.ConnectVoice(safeNick);
            //    Debug.Log($"[PlayerNameSync] 플레이어 '{safeNick}'의 닉네임으로 보이스 채팅 서버에 연결 요청을 보냈습니다.");
            //}

        }

        NetworkNickname.OnValueChanged += OnNicknameChanged;

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

        OnplayerRosterChanged.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        NetworkNickname.OnValueChanged -= OnNicknameChanged;
        OnplayerRosterChanged.Invoke();
    }

    // 클라이언트가 서버에 닉네임 설정을 요청하는 통로
    [ServerRpc]
    void SetNicknameServerRpc(string newName)
    {
        // 서버 권한으로 네트워크 변수 값을 변경 (이때 모든 클라이언트에 동기화됨)
        NetworkNickname.Value = newName;
    }

    void OnNicknameChanged(FixedString64Bytes oldV, FixedString64Bytes newV)
    {
        string nameStr = newV.ToString();
        string safeNick = nameStr.Replace("\0", "").Trim();

        // UI 갱신
        if (MultiplayerUIManager.Instance != null)
        {
            MultiplayerUIManager.Instance.RefreshPlayerList();
        }
        ApplyNicknameToUI(nameStr);

        if (!string.IsNullOrEmpty(safeNick))
        {
            OnNicknameSynced?.Invoke(safeNick, transform);
        }

        // 보이스 채팅 연결 (본인인 경우에만 최초 1회 실행되도록 로직 필요)
        if (IsOwner && GlobalVoiceManager.Instance != null)
        {
            GlobalVoiceManager.Instance.ConnectVoice(safeNick);
        }
        OnplayerRosterChanged.Invoke();
    }

    void ApplyNicknameToUI(string name)
    {
        if (MultiplayerUIManager.Instance != null)
        {
            int slotIndex = (int)OwnerClientId;
            MultiplayerUIManager.Instance.UpdatePlayerName(slotIndex, name, IsOwner);
        }
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