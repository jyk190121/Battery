using Photon.Realtime;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalVoiceManager : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
{
    public static GlobalVoiceManager Instance;

    public UnityVoiceClient globalVoiceClient;
    public Recorder globalRecorder;
    private string globalRoomName = "Global_Main_Room";

    // [핵심] 플레이어들의 스피커(AudioSource)를 닉네임으로 찾기 쉽게 저장해두는 딕셔너리
    private Dictionary<string, AudioSource> playerAudioSources = new Dictionary<string, AudioSource>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (globalVoiceClient == null) globalVoiceClient = GetComponent<UnityVoiceClient>();
        if (globalRecorder == null) globalRecorder = GetComponent<Recorder>();

        globalVoiceClient.PrimaryRecorder = globalRecorder;

        // 마이크는 현장 소음을 위해 항상 켜둡니다! (V키 누를 필요 없음)
        globalRecorder.TransmitEnabled = true;

        globalVoiceClient.Client.AddCallbackTarget(this);
        globalVoiceClient.SpeakerLinked += OnSpeakerLinked;

        globalVoiceClient.ConnectUsingSettings();
    }

    private void OnDestroy()
    {
        if (globalVoiceClient != null)
        {
            globalVoiceClient.Client.RemoveCallbackTarget(this);
            globalVoiceClient.SpeakerLinked -= OnSpeakerLinked;
        }
    }

    public void OnConnectedToMaster()
    {
        globalVoiceClient.Client.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = globalRoomName });
    }

    private void OnSpeakerLinked(Speaker speaker)
    {
        int playerId = speaker.RemoteVoice.PlayerId;
        speaker.gameObject.name = $"Global_Speaker_{playerId}";

        AudioSource aud = speaker.GetComponent<AudioSource>();
        if (aud != null)
        {
            aud.spatialBlend = 1f; // 기본은 3D 현장 사운드
            aud.minDistance = 2f;
            aud.maxDistance = 20f;
            aud.rolloffMode = AudioRolloffMode.Linear;
            aud.playOnAwake = true;
        }

        StartCoroutine(AttachSpeakerToPlayer(speaker, playerId, aud));
    }

    private IEnumerator AttachSpeakerToPlayer(Speaker speaker, int photonPlayerId, AudioSource aud)
    {
        yield return new WaitForSeconds(0.5f);

        Photon.Realtime.Player photonPlayer = globalVoiceClient.Client.CurrentRoom.GetPlayer(photonPlayerId);
        if (photonPlayer == null) yield break;

        string targetNick = photonPlayer.NickName;

        // 딕셔너리에 대상의 닉네임과 오디오소스를 저장해둡니다.
        if (!playerAudioSources.ContainsKey(targetNick))
        {
            playerAudioSources.Add(targetNick, aud);
        }

        PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.NetworkNickname.Value.ToString() == targetNick)
            {
                speaker.transform.SetParent(p.transform);
                speaker.transform.localPosition = new Vector3(0, 1.5f, 0);
                yield break;
            }
        }
    }

    // [핵심 기능] 통화가 연결되거나 끊어질 때 OnCallingUI에서 이 함수를 호출합니다.
    public void SetCallMode(string targetNickname, bool isCalling)
    {
        if (playerAudioSources.TryGetValue(targetNickname, out AudioSource aud))
        {
            if (aud != null)
            {
                // 전화 중이면 0(2D), 평소엔 1(3D) 로 전환합니다.
                aud.spatialBlend = isCalling ? 0f : 1f;
                Debug.Log($"[Voice] {targetNickname}의 스피커를 {(isCalling ? "2D(전화)" : "3D(현장)")} 모드로 즉시 변경했습니다.");
            }
        }
    }

    // 필수 인터페이스 구현부 (생략)
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