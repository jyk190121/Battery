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

    // AudioSource 대신 복제 스크립트(VoiceCopier)를 저장합니다.
    private Dictionary<string, VoiceCopier> playerVoices = new Dictionary<string, VoiceCopier>();
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
        }

        // 투 트랙 출력을 위해 복제 컴포넌트를 달아줍니다.
        VoiceCopier copier = speaker.gameObject.AddComponent<VoiceCopier>();
        copier.Init(aud3D);

        StartCoroutine(AttachSpeakerToPlayer(speaker, playerId, copier));
    }

    private IEnumerator AttachSpeakerToPlayer(Speaker speaker, int photonPlayerId, VoiceCopier copier)
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

        if (!playerVoices.ContainsKey(targetNick))
        {
            playerVoices.Add(targetNick, copier);
        }

        if (callStateDict.TryGetValue(targetNick, out bool isCalling) && isCalling)
        {
            copier.SetCall(isCalling);
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

        if (playerVoices.TryGetValue(targetNickname, out VoiceCopier copier))
        {
            if (copier != null)
            {
                copier.SetCall(isCalling);
                Debug.Log($"[Voice] {targetNickname}의 통화(2D) 모드가 {(isCalling ? "켜졌습니다" : "꺼졌습니다")}. 3D 육성은 유지됩니다.");
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

// ========================================================================
// 3D 오디오 스트림을 2D 스피커로 복제해주는 컴포넌트
// ========================================================================
public class VoiceCopier : MonoBehaviour
{
    private AudioSource aud3D;
    private AudioSource aud2D;

    public void Init(AudioSource main3D)
    {
        aud3D = main3D;

        // 2D 전용 스피커를 하나 더 만듭니다.
        aud2D = gameObject.AddComponent<AudioSource>();
        aud2D.spatialBlend = 0f; // 완벽한 2D 평면 소리
        aud2D.volume = 0f;       // 기본적으로는 꺼둠 (전화 올 때만 켜짐)
    }

    void Update()
    {
        if (aud3D == null || aud2D == null) return;

        // 포톤이 3D 오디오에 실시간으로 클립을 할당하면, 2D 오디오도 똑같이 가져와서 틉니다.
        if (aud3D.clip != null && aud2D.clip != aud3D.clip)
        {
            aud2D.clip = aud3D.clip;
            aud2D.loop = true;
            aud2D.Play();
        }

        // 재생 싱크(타이밍) 동기화
        if (aud3D.isPlaying && aud2D.isPlaying)
        {
            if (Mathf.Abs(aud2D.timeSamples - aud3D.timeSamples) > 1000)
            {
                aud2D.timeSamples = aud3D.timeSamples;
            }
        }
    }

    public void SetCall(bool isCalling)
    {
        // 볼륨 조절 없이 100% 출력
        if (aud2D != null) aud2D.volume = isCalling ? 1f : 0f;
    }
}