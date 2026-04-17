using Photon.Realtime;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Audio;

public class GlobalVoiceManager : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
{
    public static GlobalVoiceManager Instance;

    public UnityVoiceClient globalVoiceClient;
    public Recorder globalRecorder;
    private string globalRoomName = "Global_Main_Room";

    [Header("Audio Settings")]
    public AudioMixerGroup phoneMixerGroup;

    // AudioSource 대신 복제 스크립트(VoiceCopier)를 저장합니다.
    private Dictionary<string, AvatarVoiceSender> playerVoices = new Dictionary<string, AvatarVoiceSender>();
    private Dictionary<string, bool> callStateDict = new Dictionary<string, bool>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (globalVoiceClient == null) globalVoiceClient = GetComponent<UnityVoiceClient>();
        if (globalRecorder == null) globalRecorder = GetComponent<Recorder>();

        globalVoiceClient.PrimaryRecorder = globalRecorder;
        globalRecorder.TransmitEnabled = false;

        globalVoiceClient.Client.AddCallbackTarget(this);
        globalVoiceClient.SpeakerLinked += OnSpeakerLinked;

        Debug.Log("[Global Voice] 매니저 대기 중... 아바타 스폰을 기다립니다.");
    }

    public void ConnectVoice(string myNickname)
    {
        Debug.Log($"[Global Voice] 아바타 스폰 확인 완료! '{myNickname}'(으)로 보이스 서버 접속 시작.");
        globalVoiceClient.Client.NickName = myNickname;
        globalVoiceClient.ConnectUsingSettings();
    }

    private void Update()
    {
        if (Keyboard.current == null || globalRecorder == null) return;

        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isInputBlocked)
        {
            if (globalRecorder.TransmitEnabled) globalRecorder.TransmitEnabled = false;
            return;
        }

        bool isVPressed = Keyboard.current.vKey.isPressed;
        if (globalRecorder.TransmitEnabled != isVPressed)
        {
            globalRecorder.TransmitEnabled = isVPressed;
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

    public void OnConnectedToMaster()
    {
        globalVoiceClient.Client.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = globalRoomName });
    }

    private void OnSpeakerLinked(Speaker speaker)
    {
        int playerId = speaker.RemoteVoice.PlayerId;
        speaker.gameObject.name = $"Global_Speaker_{playerId}";

        AudioSource aud3D = speaker.GetComponent<AudioSource>();
        if (aud3D != null)
        {
            aud3D.spatialBlend = 1f; // 3D 사운드는 항상 유지됩니다.
            aud3D.minDistance = 2f;
            aud3D.maxDistance = 20f;
            aud3D.rolloffMode = AudioRolloffMode.Linear;
            aud3D.playOnAwake = true;

            // [추가] 거리가 멀어져도 오디오 연산을 강제로 끄지 못하게 만듦
            aud3D.priority = 0; // 최고 우선순위 부여
        }

        // 투 트랙 출력을 위해 복제 컴포넌트를 달아줍니다.
        AvatarVoiceSender sender = speaker.gameObject.AddComponent<AvatarVoiceSender>();
        StartCoroutine(AttachSpeakerToPlayer(speaker, playerId, sender));
    }

    private IEnumerator AttachSpeakerToPlayer(Speaker speaker, int photonPlayerId, AvatarVoiceSender sender)
    {
        Debug.Log($"[Global Voice] (1/4) 부착 프로세스 시작! 대상의 Voice ID: {photonPlayerId}");

        Photon.Realtime.Player photonPlayer = null;
        int infoRetries = 0;

        while (photonPlayer == null && infoRetries < 10)
        {
            if (globalVoiceClient.Client.InRoom)
            {
                photonPlayer = globalVoiceClient.Client.CurrentRoom.GetPlayer(photonPlayerId);
            }

            if (photonPlayer == null)
            {
                yield return new WaitForSeconds(0.2f);
                infoRetries++;
            }
        }

        if (photonPlayer == null)
        {
            Debug.LogError($"[Global Voice] (에러) {photonPlayerId}번 유저의 접속 정보를 찾지 못했습니다.");
            yield break;
        }

        string rawNick = string.IsNullOrEmpty(photonPlayer.NickName) ? "Guest" : photonPlayer.NickName;
        string targetNick = rawNick.Replace("\0", "").Trim();

        // 딕셔너리에 추가할 때 copier 대신 sender를 넣습니다.
        if (!playerVoices.ContainsKey(targetNick))
        {
            playerVoices.Add(targetNick, sender);
        }

        // 통화 상태 동기화 시 SetCall 대신 SetCallMode를 호출합니다.
        if (callStateDict.TryGetValue(targetNick, out bool isCalling) && isCalling)
        {
            sender.SetCallMode(isCalling);
        }

        bool isAttached = false;
        int maxRetries = 20;
        int retries = 0;

        while (!isAttached && retries < maxRetries)
        {
            PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                if (p == null) continue;

                string netcodeNick = "";
                try { netcodeNick = p.NetworkNickname.Value.ToString().Replace("\0", "").Trim(); }
                catch { continue; }

                if (netcodeNick == targetNick)
                {
                    speaker.transform.SetParent(p.transform);
                    speaker.transform.localPosition = new Vector3(0, 1.5f, 0);

                    Debug.Log($"[Global Voice] (4/4 성공!) {targetNick}의 아바타에 스피커 부착 완료.");
                    isAttached = true;
                    break;
                }
            }

            if (!isAttached)
            {
                retries++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (!isAttached)
        {
            Debug.LogWarning($"[Global Voice] (실패) '{targetNick}'인 플레이어가 없습니다.");
        }
    }

    public void SetCallMode(string targetNickname, bool isCalling)
    {
        callStateDict[targetNickname] = isCalling;

        if (playerVoices.TryGetValue(targetNickname, out AvatarVoiceSender sender))
        {
            if (sender != null)
            {
                sender.SetCallMode(isCalling);
                Debug.Log($"[Voice DSP] {targetNickname}의 파형 복제 통화 모드가 {(isCalling ? "켜졌습니다" : "꺼졌습니다")}.");
            }
        }
    }

    // ========================================================================
    // 필수 구현 빈 함수들
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