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
        copier.Init(aud3D, phoneMixerGroup);

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
// 3D 오디오 스트림을 2D 스피커로 복제해주는 컴포넌트 (원상 복구)
// ========================================================================
public class VoiceCopier : MonoBehaviour
{
    private AudioSource aud3D;
    private AudioMixerGroup phoneMixer;
    private AudioMixerGroup defaultMixer;

    private bool isCalling = false;

    // 원래 3D 사운드 거리 설정 백업용
    private float originalMinDistance;
    private float originalMaxDistance;

    public void Init(AudioSource main3D, AudioMixerGroup mixerGroup)
    {
        aud3D = main3D;
        phoneMixer = mixerGroup;
        defaultMixer = main3D.outputAudioMixerGroup;

        // 원래 설정된 거리 백업 (기본값 min:2, max:20)
        originalMinDistance = aud3D.minDistance;
        originalMaxDistance = aud3D.maxDistance;

        // 무조건 3D 고정
        aud3D.spatialBlend = 1f;
    }

    void Update()
    {
        if (aud3D == null) return;

        if (isCalling)
        {
            // [전화 중] 
            // 1. 오디오 믹서 필터 씌우기
            if (aud3D.outputAudioMixerGroup != phoneMixer)
                aud3D.outputAudioMixerGroup = phoneMixer;

            // 2. 방향(3D)은 유지하되, 거리가 멀어도 소리가 작아지지 않게 인식 범위 극대화
            if (aud3D.maxDistance != 5000f)
            {
                aud3D.maxDistance = 5000f;
                aud3D.minDistance = 5000f; // min을 max와 동일하게 맞추면 거리 비례 볼륨 감소가 일어너지 않음
            }
        }
        else
        {
            // [평상시] 
            // 1. 기본 믹서(생목소리)로 복구
            if (aud3D.outputAudioMixerGroup != defaultMixer)
                aud3D.outputAudioMixerGroup = defaultMixer;

            // 2. 원래의 거리 감쇄(가까워야만 들림)로 복구
            if (aud3D.maxDistance != originalMaxDistance)
            {
                aud3D.maxDistance = originalMaxDistance;
                aud3D.minDistance = originalMinDistance;
            }
        }
    }

    public void SetCall(bool state)
    {
        isCalling = state;
    }
}