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
    private AudioSource aud3D; // 포톤이 제공하는 단 1개의 원본 오디오 소스
    private AudioMixerGroup phoneMixer;
    private AudioMixerGroup defaultMixer; // 원래의 믹서

    private Transform listener;
    private bool isCalling = false;

    [Header("거리 설정")]
    public float transitionDistance = 10f; // 3D 전환 거리 (이 안으로 오면 생목소리가 섞임)

    public void Init(AudioSource main3D, AudioMixerGroup mixerGroup)
    {
        aud3D = main3D;
        phoneMixer = mixerGroup;
        defaultMixer = main3D.outputAudioMixerGroup; // 기존 믹서 백업

        // 내 캐릭터의 귀(일반적으로 메인 카메라)를 찾음
        if (Camera.main != null)
        {
            listener = Camera.main.transform;
        }

        // 주의: 더 이상 aud2D(복제본)를 AddComponent 하지 않습니다!
    }

    void Update()
    {
        if (aud3D == null || listener == null) return;

        // 1. 전화 중이 아닐 때: 무조건 100% 3D 사운드 유지
        if (!isCalling)
        {
            if (aud3D.spatialBlend != 1f) aud3D.spatialBlend = 1f;
            if (aud3D.outputAudioMixerGroup != defaultMixer) aud3D.outputAudioMixerGroup = defaultMixer;
            return;
        }

        // 2. 전화 중일 때: 거리에 따른 2D / 3D 동적 믹싱
        float distance = Vector3.Distance(transform.position, listener.position);

        if (distance > transitionDistance)
        {
            // 멀리 있을 때: 100% 2D (귀에 대고 듣는 선명한 전화 소리)
            aud3D.spatialBlend = 0f;
            if (aud3D.outputAudioMixerGroup != phoneMixer) aud3D.outputAudioMixerGroup = phoneMixer;
        }
        else
        {
            // 가까이 다가올 때: 2D에서 3D로 서서히 전환되며 겹침
            float t = 1f - (distance / transitionDistance);
            aud3D.spatialBlend = Mathf.Clamp01(t);

            // 상대방이 매우 가까워지면 전화기 필터를 끄고 생목소리로 전환
            if (distance < transitionDistance * 0.5f)
            {
                if (aud3D.outputAudioMixerGroup != defaultMixer) aud3D.outputAudioMixerGroup = defaultMixer;
            }
            else
            {
                if (aud3D.outputAudioMixerGroup != phoneMixer) aud3D.outputAudioMixerGroup = phoneMixer;
            }
        }
    }

    public void SetCall(bool state)
    {
        isCalling = state;
    }
}