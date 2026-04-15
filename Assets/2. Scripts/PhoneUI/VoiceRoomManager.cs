using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;

public class VoiceRoomManager : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
{
    public static VoiceRoomManager Instance;
    public UnityVoiceClient voiceClient;
    public Recorder callRecorder; // [추가] 명시적 리코더 연결

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (voiceClient == null) voiceClient = GetComponent<UnityVoiceClient>();
        if (callRecorder == null) callRecorder = GetComponent<Recorder>();

        // [중요] 해당 클라이언트의 메인 마이크로 지정 후 평소엔 꺼두기
        voiceClient.PrimaryRecorder = callRecorder;
        callRecorder.TransmitEnabled = false;

        voiceClient.Client.AddCallbackTarget(this);
        voiceClient.SpeakerLinked += OnSpeakerLinked;

        Debug.Log("[Voice] 보이스 서버에 접속을 시도합니다...");
        voiceClient.ConnectUsingSettings();
    }

    private void OnDestroy()
    {
        if (voiceClient != null)
        {
            voiceClient.Client.RemoveCallbackTarget(this);
            voiceClient.SpeakerLinked -= OnSpeakerLinked;
        }
    }

    public void JoinCallRoom(string userA, string userB)
    {
        string roomName = string.Compare(userA, userB) < 0 ? $"Call_{userA}_{userB}" : $"Call_{userB}_{userA}";
        voiceClient.Client.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = roomName });
    }

    public void LeaveCallRoom()
    {
        if (voiceClient != null && voiceClient.Client.InRoom)
        {
            voiceClient.Client.OpLeaveRoom(false);
        }
    }

    private void OnSpeakerLinked(Speaker speaker)
    {
        int playerId = speaker.RemoteVoice.PlayerId;
        speaker.gameObject.name = $"Call_Speaker_{playerId}";

        AudioSource aud = speaker.GetComponent<AudioSource>();
        if (aud != null)
        {
            aud.spatialBlend = 0f;
            aud.playOnAwake = true;
        }
    }

    public void OnConnectedToMaster() { }
    public void OnJoinedRoom() { }
    public void OnConnected() { }
    public void OnDisconnected(DisconnectCause cause) { }
    public void OnRegionListReceived(RegionHandler regionHandler) { }
    public void OnCustomAuthenticationResponse(System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string debugMessage) { }
    public void OnFriendListUpdate(System.Collections.Generic.List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { }
}