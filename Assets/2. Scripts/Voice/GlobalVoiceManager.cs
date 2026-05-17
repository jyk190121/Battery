using Photon.Realtime;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Key = UnityEngine.InputSystem.Key;

public class GlobalVoiceManager : MonoBehaviour, IConnectionCallbacks
{
    public static GlobalVoiceManager Instance;

    public UnityVoiceClient globalVoiceClient;
    public Recorder globalRecorder;
    private string currentRoomName = "";
    float _autoJoinTimer = 0f;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("<color=cyan>[Voice]</color> GlobalVoiceManager를 Root로 이동 및 보존 완료.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (globalVoiceClient == null) globalVoiceClient = GetComponent<UnityVoiceClient>();
        if (globalRecorder == null) globalRecorder = GetComponent<Recorder>();

        globalVoiceClient.PrimaryRecorder = globalRecorder;

        globalRecorder.RecordingEnabled = true;

        globalRecorder.TransmitEnabled = false;

        // [수정] 두 가지 콜백을 모두 등록합니다.
        globalVoiceClient.Client.AddCallbackTarget(this);
        globalVoiceClient.SpeakerLinked += OnSpeakerLinked;
    }

    private void Update()
    {
        if (globalVoiceClient == null || globalVoiceClient.Client == null) return;

        // [기존 자가 치유 로직 유지] ...
        if (globalVoiceClient.Client.State == ClientState.ConnectedToMasterServer && !globalVoiceClient.Client.InRoom)
        {
            _autoJoinTimer += Time.deltaTime;
            if (_autoJoinTimer > 2.0f)
            {
                JoinVoiceRoomInternal();
                _autoJoinTimer = 0f;
            }
        }
        else
        {
            _autoJoinTimer = 0f;
        }

        // =======================================================
        // 💡 [핵심 추가] Push-To-Talk (V키) 마이크 송출 스위치 로직
        // =======================================================
        if (globalRecorder != null)
        {
            // 방에 들어와 있을 때만 V키 작동
            if (globalVoiceClient.Client.InRoom)
            {
                // V키 누르는 순간 -> 마이크 ON
                if (UnityEngine.InputSystem.Keyboard.current.vKey.wasPressedThisFrame)
                {
                    globalRecorder.TransmitEnabled = true;
                    Debug.Log("<color=green>[Mic]</color> 마이크 송출 켜짐 (V키 누름)");
                }
                // V키 떼는 순간 -> 마이크 OFF
                else if (UnityEngine.InputSystem.Keyboard.current.vKey.wasReleasedThisFrame)
                {
                    globalRecorder.TransmitEnabled = false;
                    Debug.Log("<color=gray>[Mic]</color> 마이크 송출 꺼짐 (V키 뗌)");
                }
            }
            else
            {
                // 방에 없는데 V키를 누르면 경고 로그
                if (UnityEngine.InputSystem.Keyboard.current.vKey.wasPressedThisFrame)
                {
                    if (globalVoiceClient.Client.State == ClientState.PeerCreated || globalVoiceClient.Client.State == ClientState.Disconnected) return;
                    Debug.LogWarning($"<color=red>[Mic Fail]</color> 아직 보이스 룸에 입장하지 않았습니다. 현재 서버 상태: {globalVoiceClient.Client.State}");
                }
            }
        }
    }

    public void ConnectVoice(string nickname, string roomName)
    {

        currentRoomName = roomName;
        globalVoiceClient.Client.NickName = nickname;

        if(globalVoiceClient.Client.State == ClientState.PeerCreated ||
            globalVoiceClient.Client.State == ClientState.Disconnected)
        {
            print("보이스 서버 신규접속 시도");
            globalVoiceClient.ConnectUsingSettings();
        }

        else if(globalVoiceClient.Client.Server == ServerConnection.MasterServer)
        {
            JoinVoiceRoomInternal();
        }

    }
   
    public void ShutdownVoice()
    {
        if (globalVoiceClient != null)
        {
            globalRecorder.RecordingEnabled = false;
            globalRecorder.TransmitEnabled = false;
        }

        if (globalVoiceClient.Client.IsConnected)
        {
            globalVoiceClient.Client.Disconnect();
        }

        currentRoomName = "";
        Debug.Log("<color=red>[Voice]</color> 시스템 셧다운 및 마이크 해제 완료");
    }

    // ==========================================
    // [관문 3] 드디어 작동할 방 입장 콜백!
    // ==========================================
    public void OnJoinedRoom()
    {
        Debug.Log($"<color=#55FF55>[Voice-3]</color> 보이스 방 입장 성공! ({globalVoiceClient.Client.CurrentRoom.Name})");
    }
    public void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"<color=red>[Voice Error] 보이스 서버 연결 끊김! 원인: 방 파괴");
    }

    private void OnSpeakerLinked(Speaker speaker)
    {

        int playerId = speaker.RemoteVoice.PlayerId;
        speaker.gameObject.name = $"Global_Speaker_{playerId}";

        AudioSource aud = speaker.GetComponent<AudioSource>();
        if (aud != null)
        {
            aud.spatialBlend = 0f;  // 2D 사운드
            aud.volume = 1f;
            aud.mute = false;
            aud.playOnAwake = true;
        }

        print($"상대방 Player {playerId} 의 스피커 셋팅 완료");

    }
 

    public void SetCallMode(string targetNickname, bool isCalling)
    {
        // 닉네임 공백, 널문자 제거
        string clearNick = targetNickname.Replace("\0", "").Trim();

        VoiceController[] controllers = FindObjectsByType<VoiceController>(FindObjectsSortMode.None);
        foreach (var vc in controllers)
        {
            bool isProximityVoice = vc.gameObject.name == $"VoiceSpeaker_{clearNick}";

            if(isProximityVoice)
            {
                vc.SetCallMode(isCalling);

                if (globalRecorder != null) globalRecorder.TransmitEnabled = isCalling;
            }
        }
    }

    private void OnDestroy()
    {
        if (globalVoiceClient != null)
        {
            globalVoiceClient.Client.RemoveCallbackTarget(this);
            globalVoiceClient.SpeakerLinked -= OnSpeakerLinked;
        }
    }

    // 💡 1. 락(Lock)을 강제로 풀어주는 콜백들 (방 입장 실패 시 영원히 멈추는 현상 방지)
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { }

    // 빈 인터페이스 구현부들
    public void OnConnected() { }
    public void OnRegionListReceived(RegionHandler regionHandler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string debugMessage) { }
  
    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    #region 씬이동 시 참조 파괴 방지 스크립트

    // 마스터 서버 연결 완료 시 호출되는 콜백
    public void OnConnectedToMaster()
    {
        JoinVoiceRoomInternal();
    }

    // 실제 방 입장 로직을 별도 함수로 분리
    void JoinVoiceRoomInternal()
    {
        if (globalVoiceClient == null || globalVoiceClient.Client == null) return;

        // 이미 방에 있거나, 마스터 서버가 아니면 무시
        if (globalVoiceClient.Client.InRoom) return;
        if (globalVoiceClient.Client.State != ClientState.ConnectedToMasterServer) return;

        // 억지로라도 방 이름 가져오기
        if (string.IsNullOrEmpty(currentRoomName) || currentRoomName == "LobbyChannel")
        {
            if (MultiPlayerSessionManager.Instance != null && !string.IsNullOrEmpty(MultiPlayerSessionManager.Instance.CurrentChannelId))
                currentRoomName = MultiPlayerSessionManager.Instance.CurrentChannelId;
            else
                currentRoomName = "GameRoom_999"; // 최후의 보루
        }

        Debug.Log($"<color=cyan>[Voice]</color> '{currentRoomName}' 방으로 입장을 시도합니다!");

        EnterRoomParams enterRoom = new EnterRoomParams
        {
            RoomName = currentRoomName,
            RoomOptions = new RoomOptions { MaxPlayers = 4 },
            Lobby = TypedLobby.Default
        };
        globalVoiceClient.Client.OpJoinOrCreateRoom(enterRoom);
    }

    #endregion
}