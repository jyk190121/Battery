using Photon.Realtime;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GlobalVoiceManager : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
{
    public static GlobalVoiceManager Instance;

    public UnityVoiceClient globalVoiceClient;
    public Recorder globalRecorder;
    private string globalRoomName = "Global_Main_Room";

    private Dictionary<string, AudioSource> playerAudioSources = new Dictionary<string, AudioSource>();
    private Dictionary<string, bool> callStateDict = new Dictionary<string, bool>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (globalVoiceClient == null) globalVoiceClient = GetComponent<UnityVoiceClient>();
        if (globalRecorder == null) globalRecorder = GetComponent<Recorder>();

        if (MultiPlayerSessionManager.Instance != null)
        {
            globalVoiceClient.Client.NickName = MultiPlayerSessionManager.Instance.PlayerNickname;
        }

        globalVoiceClient.PrimaryRecorder = globalRecorder;
        globalRecorder.TransmitEnabled = false;

        globalVoiceClient.Client.AddCallbackTarget(this);
        globalVoiceClient.SpeakerLinked += OnSpeakerLinked;

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

        AudioSource aud = speaker.GetComponent<AudioSource>();
        if (aud != null)
        {
            aud.spatialBlend = 1f;
            aud.minDistance = 2f;
            aud.maxDistance = 20f;
            aud.rolloffMode = AudioRolloffMode.Linear;
            aud.playOnAwake = true;
        }

        // [강화됨] 스피커 부착 코루틴 실행
        StartCoroutine(AttachSpeakerToPlayer(speaker, playerId, aud));
    }

    // ========================================================================
    // 스피커를 아바타에 확실하게 붙이는 추적 코루틴
    // ========================================================================
    private IEnumerator AttachSpeakerToPlayer(Speaker speaker, int photonPlayerId, AudioSource aud)
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

        // 포톤 유저를 끝내 못 찾았을 경우 안전하게 종료
        if (photonPlayer == null)
        {
            Debug.LogError($"[Global Voice] (에러) {photonPlayerId}번 유저의 포톤 접속 정보를 찾지 못해 부착을 포기합니다.");
            yield break;
        }

        Debug.Log($"[Global Voice] (2/4) 포톤 유저 정보 획득 성공. 서버상 닉네임: '{photonPlayer.NickName}'");

        // [에러 방지 1] 만약 닉네임이 null 이면 빈칸 처리하여 코드 즉사를 막음
        string rawNick = string.IsNullOrEmpty(photonPlayer.NickName) ? "Guest" : photonPlayer.NickName;
        string targetNick = rawNick.Replace("\0", "").Trim();

        if (!playerAudioSources.ContainsKey(targetNick))
        {
            playerAudioSources.Add(targetNick, aud);
        }

        if (callStateDict.TryGetValue(targetNick, out bool isCalling) && isCalling)
        {
            aud.spatialBlend = 0f;
        }

        Debug.Log($"[Global Voice] (3/4) 세척된 닉네임 '{targetNick}'의 아바타를 맵에서 찾기 시작합니다.");

        bool isAttached = false;
        int maxRetries = 20;
        int retries = 0;

        while (!isAttached && retries < maxRetries)
        {
            PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                if (p == null) continue;

                // [에러 방지 2] 넷코드 닉네임 변환 중 에러가 나도 코루틴이 죽지 않게 방어
                string netcodeNick = "";
                try
                {
                    netcodeNick = p.NetworkNickname.Value.ToString().Replace("\0", "").Trim();
                }
                catch
                {
                    continue; // 변환 실패 시 무시하고 다음 사람 검색
                }

                if (netcodeNick == targetNick)
                {
                    speaker.transform.SetParent(p.transform);
                    speaker.transform.localPosition = new Vector3(0, 1.5f, 0);

                    Debug.Log($"[Global Voice] (4/4 성공!) {targetNick}의 아바타를 찾아 3D 스피커 부착 완료! (탐색 횟수: {retries + 1}회)");
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
            Debug.LogWarning($"[Global Voice] (실패) 10초 동안 찾았지만 맵 위에 닉네임이 '{targetNick}'인 플레이어가 없습니다. 스피커가 허공에 남습니다.");
        }
    }
    // ========================================================================

    public void SetCallMode(string targetNickname, bool isCalling)
    {
        callStateDict[targetNickname] = isCalling;

        if (playerAudioSources.TryGetValue(targetNickname, out AudioSource aud))
        {
            if (aud != null)
            {
                aud.spatialBlend = isCalling ? 0f : 1f;
                Debug.Log($"[Voice] {targetNickname}의 스피커가 {(isCalling ? "2D(전화)" : "3D(현장)")} 모드로 변경되었습니다.");
            }
        }
    }

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