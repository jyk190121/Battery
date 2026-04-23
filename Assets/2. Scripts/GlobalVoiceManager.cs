using Photon.Realtime;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class GlobalVoiceManager : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
{
    public static GlobalVoiceManager Instance;

    public UnityVoiceClient globalVoiceClient;
    public Recorder globalRecorder;
    private string globalRoomName = "Global_Main_Room";

    [Header("Audio Settings")]
    public AudioMixerGroup phoneMixerGroup;

    // AudioSource 대신 복제 스크립트(VoiceCopier)를 저장합니다.
    //private Dictionary<string, AvatarVoiceSender> playerVoices = new Dictionary<string, AvatarVoiceSender>();
    Dictionary<string, VoiceCopier> playerVoices = new Dictionary<string, VoiceCopier>();
    private Dictionary<string, bool> callStateDict = new Dictionary<string, bool>();

    private Dictionary<string, Speaker> pendingSpeakers = new Dictionary<string, Speaker>();

    private void Awake()
    {
        // [수정] 싱글톤 사용
        if (Instance == null) 
        { 
            Instance = this; DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        // [추가] 닉네임 동기화 완료 이벤트 구독
        PlayerNameSync.OnNicknameSynced += HandleAvatarReady;
    }

    private void OnDisable()
    {
        // [추가] 이벤트 구독 해제
        PlayerNameSync.OnNicknameSynced -= HandleAvatarReady;
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
        if (Keyboard.current == null || globalRecorder == null || !globalVoiceClient.Client.InRoom) return;

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
        //speaker.gameObject.name = $"Global_Speaker_{playerId}";
        Photon.Realtime.Player remotePlayer = globalVoiceClient.Client.CurrentRoom.GetPlayer(playerId);

        if (remotePlayer == null) return;

        string targetNick = remotePlayer.NickName.Replace("\0", "").Trim();
        speaker.gameObject.name = $"Global_Speaker_{targetNick}";

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

        VoiceCopier copier = speaker.gameObject.GetOrAddComponent<VoiceCopier>();
        copier.Init(aud3D, phoneMixerGroup);

        playerVoices[targetNick] = copier;
        if (callStateDict.TryGetValue(targetNick, out bool isCalling))
        {
            copier.SetCall(isCalling);
        }


        // 투 트랙 출력을 위해 복제 컴포넌트를 달아줍니다.
        //AvatarVoiceSender sender = speaker.gameObject.AddComponent<AvatarVoiceSender>();
        //StartCoroutine(AttachSpeakerToPlayer(speaker, targetNick));
        TryAttachOrWait(speaker, targetNick);
    }

    private void TryAttachOrWait(Speaker speaker, string targetNick)
    {
        // 1. 현재 씬에 이미 닉네임 동기화가 끝난 아바타가 있는지 먼저 찾아봅니다.
        PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p == null) continue;

            string netcodeNick = p.NetworkNickname.Value.ToString().Replace("\0", "").Trim();
            if (netcodeNick.Equals(targetNick, StringComparison.OrdinalIgnoreCase))
            {
                // 아바타가 이미 준비되어 있다면 즉시 부착
                AttachSpeakerDirectly(speaker, p.transform, targetNick);
                return;
            }
        }

        // 2. 아직 아바타가 없거나 닉네임 동기화가 안 끝났다면 대기열에 등록합니다.
        Debug.Log($"<color=yellow>[Global Voice] {targetNick} 아바타 대기 중... 동기화 이벤트 수신 대기</color>");
        pendingSpeakers[targetNick] = speaker;
    }

    private void HandleAvatarReady(string targetNick, Transform avatarTransform)
    {
        // PlayerNameSync 쪽에서 "나 닉네임 동기화 끝났어!" 라고 이벤트를 쏘면 여기로 들어옵니다.
        if (pendingSpeakers.TryGetValue(targetNick, out Speaker pendingSpeaker))
        {
            if (pendingSpeaker != null)
            {
                AttachSpeakerDirectly(pendingSpeaker, avatarTransform, targetNick);
            }
            // 부착 완료 후 대기열에서 제거
            pendingSpeakers.Remove(targetNick);
        }
    }

    private void AttachSpeakerDirectly(Speaker speaker, Transform parentTransform, string targetNick)
    {
        speaker.transform.SetParent(parentTransform);
        speaker.transform.localPosition = new Vector3(0, 1.8f, 0.2f);
        Debug.Log($"<color=green>[Global Voice] {targetNick} 아바타 부착 성공! (이벤트 방식)</color>");
    }

    // [수정]
    //private IEnumerator AttachSpeakerToPlayer(Speaker speaker, string targetNick)
    //{
    //    //Debug.Log($"[Global Voice] (1/4) 부착 프로세스 시작! 대상의 Voice ID: {photonPlayerId}");

    //    //Photon.Realtime.Player photonPlayer = null;
    //    //int infoRetries = 0;

    //    //while (photonPlayer == null && infoRetries < 10)
    //    //{
    //    //    if (globalVoiceClient.Client.InRoom)
    //    //    {
    //    //        photonPlayer = globalVoiceClient.Client.CurrentRoom.GetPlayer(photonPlayerId);
    //    //    }

    //    //    if (photonPlayer == null)
    //    //    {
    //    //        yield return new WaitForSeconds(0.2f);
    //    //        infoRetries++;
    //    //    }
    //    //}

    //    //if (photonPlayer == null)
    //    //{
    //    //    Debug.LogError($"[Global Voice] (에러) {photonPlayerId}번 유저의 접속 정보를 찾지 못했습니다.");
    //    //    yield break;
    //    //}

    //    //string rawNick = string.IsNullOrEmpty(photonPlayer.NickName) ? "Guest" : photonPlayer.NickName;
    //    //string targetNick = rawNick.Replace("\0", "").Trim();

    //    //// 딕셔너리에 추가할 때 copier 대신 sender를 넣습니다.
    //    //if (!playerVoices.ContainsKey(targetNick))
    //    //{
    //    //    playerVoices.Add(targetNick, sender);
    //    //}

    //    //// 통화 상태 동기화 시 SetCall 대신 SetCallMode를 호출합니다.
    //    //if (callStateDict.TryGetValue(targetNick, out bool isCalling) && isCalling)
    //    //{
    //    //    sender.SetCallMode(isCalling);
    //    //}

    //    //bool isAttached = false;
    //    //int maxRetries = 40;
    //    //int retries = 0;

    //    //while (!isAttached && retries < maxRetries)
    //    //{
    //    //    PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

    //    //    foreach (var p in allPlayers)
    //    //    {
    //    //        if (p == null) continue;

    //    //        string netcodeNick = "";
    //    //        try { netcodeNick = p.NetworkNickname.Value.ToString().Replace("\0", "").Trim(); }
    //    //        catch { continue; }

    //    //        if (netcodeNick == targetNick)
    //    //        {
    //    //            speaker.transform.SetParent(p.transform);
    //    //            speaker.transform.localPosition = new Vector3(0, 1.5f, 0);

    //    //            Debug.Log($"[Global Voice] (4/4 성공!) {targetNick}의 아바타에 스피커 부착 완료.");
    //    //            isAttached = true;
    //    //            break;
    //    //        }
    //    //    }

    //    //    if (!isAttached)
    //    //    {
    //    //        retries++;
    //    //        yield return new WaitForSeconds(0.5f);
    //    //    }
    //    //}

    //    //if (!isAttached)
    //    //{
    //    //    Debug.LogWarning($"[Global Voice] (실패) '{targetNick}'인 플레이어가 없습니다.");
    //    //}

    //    // [수정]
    //    bool isAttached = false;
    //    int maxRetries = 40;
    //    int retries = 0;

    //    while (!isAttached && retries < maxRetries)
    //    {
    //        PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

    //        foreach (var p in allPlayers)
    //        {
    //            if (p == null) continue;

    //            string netcodeNick = "";
    //            try
    //            {
    //                if (p.NetworkNickname != null)
    //                {
    //                    var fixedString = p.NetworkNickname.Value;
    //                    netcodeNick = fixedString.ToString().Replace("\0", "").Trim();
    //                }
    //            }
    //            catch (System.Exception e)
    //            {
    //                // 아직 동기화가 안 되었을 경우 로그를 남기지 않고 다음 프레임 기약
    //                continue;
    //            }

    //            // 3. 닉네임이 비어있으면 아직 데이터가 안 온 것이므로 대기
    //            if (string.IsNullOrEmpty(netcodeNick)) continue;

    //            if (netcodeNick.Equals(targetNick, StringComparison.OrdinalIgnoreCase))
    //            {
    //                speaker.transform.SetParent(p.transform);
    //                speaker.transform.localPosition = new Vector3(0, 1.8f, 0.2f);

    //                Debug.Log($"<color=green>[Global Voice] {targetNick} 아바타 부착 성공!</color>");
    //                isAttached = true;
    //                break;
    //            }
    //        }

    //        if (!isAttached)
    //        {
    //            retries++;
    //            // 간격을 조금 줄여서 더 자주 체크하되, 에러는 방지
    //            yield return new WaitForSeconds(0.5f);
    //        }
    //    }
    //}

    public void SetCallMode(string targetNickname, bool isCalling)
    {
        callStateDict[targetNickname] = isCalling;

        if (playerVoices.TryGetValue(targetNickname, out VoiceCopier copier))
        {
            if (copier != null) copier.SetCall(isCalling);
        }
    }

    #region 포톤 외부 호출용
    public void InitVoice(string myNickname)
    {
        globalVoiceClient.Client.NickName = myNickname;

        if (globalRecorder != null)
        {
            globalVoiceClient.PrimaryRecorder = globalRecorder;
            globalRecorder.TransmitEnabled = true;
            globalRecorder.RestartRecording();
        }

        globalVoiceClient.ConnectUsingSettings();
    }

    #endregion

    // ========================================================================
    // 필수 구현 빈 함수들
    public void OnJoinedRoom() { }
    public void OnConnected() { }
    public void OnDisconnected(DisconnectCause cause) { }
    public void OnRegionListReceived(RegionHandler regionHandler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string debugMessage) { }
    public void OnFriendListUpdate(List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { }
}

// [추가] 평소 3D 사운드 <-> 통화 시 2D 사운드처리용
public class VoiceCopier : MonoBehaviour
{
    AudioSource aud3D;
    AudioMixerGroup phoneMixer;
    AudioMixerGroup defaultMixer;

    Transform listener;
    bool isCalling = false;

    public float transitionDistance = 10f;

    public void Init(AudioSource main3D, AudioMixerGroup mixerGroup)
    {
        aud3D = main3D;
        phoneMixer = mixerGroup;
        defaultMixer = main3D.outputAudioMixerGroup;

        if (Camera.main != null) listener = Camera.main.transform;
    }

    void Update()
    {
        if (aud3D == null || listener == null) return;

        // 1. 평상시: 100% 3D
        if (!isCalling)
        {
            aud3D.spatialBlend = 1f;
            aud3D.outputAudioMixerGroup = defaultMixer;
            return;
        }

        // 2. 통화 중: 거리 기반 믹싱
        float distance = Vector3.Distance(transform.position, listener.position);

        if (distance > transitionDistance)
        {
            // 멀면 2D (전화기 소리)
            aud3D.spatialBlend = 0f;
            aud3D.outputAudioMixerGroup = phoneMixer;
        }
        else
        {
            // 가까워지면 3D로 전환 (생목소리 강조)
            float t = 1f - (distance / transitionDistance);
            aud3D.spatialBlend = Mathf.Lerp(0f, 1f, t);

            // 아주 가까우면 필터 제거
            aud3D.outputAudioMixerGroup = (distance < transitionDistance * 0.4f) ? defaultMixer : phoneMixer;
        }
    }

    public void SetCall(bool state) => isCalling = state;
   
}

// [추가] 헬퍼 확장 메서드 (미사용 시 삭제예정)
public static class ComponentExtensions
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        return comp != null ? comp : go.AddComponent<T>();
    }
}