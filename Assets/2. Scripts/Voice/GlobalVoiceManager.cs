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
    bool isConnecting = false;
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
                isConnecting = false;
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
        // 💡 [핵심 방어막] 전달받은 방 이름이 비어있거나 "LobbyChannel"이면 강제로 임시 방을 파서라도 무조건 집어넣습니다!
        if (string.IsNullOrEmpty(roomName) || roomName == "LobbyChannel")
        {
            currentRoomName = "GameRoom_999";
            Debug.LogWarning($"<color=orange>[Voice Warning]</color> 정상적인 방 이름이 오지 않아 'GameRoom_999'로 강제 설정합니다.");
        }
        else
        {
            currentRoomName = roomName;
        }

        Debug.Log($"<color=cyan>[Voice]</color> ConnectVoice 시작! 대상 룸: {currentRoomName}");
        StartCoroutine(ReconnectVoiceRoutine(nickname));
    }
    IEnumerator ReconnectVoiceRoutine(string nickname)
    {
        isConnecting = false;

        // 💡 [핵심 2] 혹시라도 방 이름이 비어있다면 MultiPlayerSessionManager에서 강제로 다시 뜯어옵니다.
        if (string.IsNullOrEmpty(currentRoomName) || currentRoomName == "LobbyChannel")
        {
            if (MultiPlayerSessionManager.Instance != null)
            {
                currentRoomName = MultiPlayerSessionManager.Instance.CurrentChannelId;
                Debug.LogWarning($"<color=orange>[Voice Warning]</color> 방 이름이 비어있어 SessionManager에서 강제로 가져왔습니다: {currentRoomName}");
            }
        }

        if (globalVoiceClient.Client.State != ClientState.Disconnected && globalVoiceClient.Client.State != ClientState.PeerCreated)
        {
            Debug.Log("<color=orange>[Voice]</color> 기존 연결을 완전히 해제합니다...");
            globalVoiceClient.Client.Disconnect();
            yield return new WaitUntil(() => globalVoiceClient.Client.State == ClientState.Disconnected || globalVoiceClient.Client.State == ClientState.PeerCreated);
        }

        yield return new WaitForSeconds(0.5f);

        if (globalRecorder != null)
        {
            globalRecorder.RecordingEnabled = false;
            globalRecorder.TransmitEnabled = false;
            globalRecorder.RestartRecording();
            Debug.Log("<color=cyan>[Voice]</color> 마이크 하드웨어 강제 리셋 완료");
        }

        globalVoiceClient.Client.LocalPlayer.NickName = nickname;
        bool connected = globalVoiceClient.ConnectUsingSettings();

        // 💡 방 이름이 정상적으로 들어갔는지 최종 확인하는 로그
        Debug.Log($"<color=#FF55FF>[Voice-1]</color> 재접속 시도 결과: {connected} / 대상 방: {currentRoomName}");
    }
    public void ShutdownVoice()
    {
        if (globalVoiceClient != null && globalVoiceClient.Client != null)
        {
            globalVoiceClient.SpeakerLinked -= OnSpeakerLinked;
            globalVoiceClient.Client.RemoveCallbackTarget(this);

            if (globalVoiceClient.Client.IsConnected)
            {
                globalVoiceClient.Client.Disconnect();
            }
        }

        if (globalRecorder != null)
        {
            globalRecorder.TransmitEnabled = false;
            // 💡 [핵심] 마이크 하드웨어의 사용 권한을 완전히 내려놓게 합니다.
            globalRecorder.RecordingEnabled = false;
        }

        StopAllCoroutines();
        currentRoomName = "";
        Debug.Log("<color=red>[Voice]</color> 시스템 셧다운 및 마이크 해제 완료");
    }

    // ==========================================
    // [관문 3] 드디어 작동할 방 입장 콜백!
    // ==========================================
    public void OnJoinedRoom()
    {
        isConnecting = false;
        Debug.Log($"<color=#55FF55>[Voice-3]</color> 보이스 방 입장 성공! ({globalVoiceClient.Client.CurrentRoom.Name})");
    }
    public void OnDisconnected(DisconnectCause cause)
    {
        isConnecting = false; // 💡 무조건 락 해제
        Debug.LogWarning($"<color=red>[Voice Error] 보이스 서버 연결 끊김! 원인: 방 파괴");
    }

    private void OnSpeakerLinked(Speaker speaker)
    {
        ////Photon.Realtime.Player remotePlayer = globalVoiceClient.Client.CurrentRoom.GetPlayer(speaker.RemoteVoice.PlayerId);
        //var remoteVoice = speaker.RemoteVoice;
        //Photon.Realtime.Player remotePlayer = globalVoiceClient.Client.CurrentRoom.GetPlayer(remoteVoice.PlayerId);

        //StartCoroutine(WaitForNickNameAndAttach(speaker, remoteVoice.PlayerId));
        //if (remotePlayer == null)
        //{
        //    Debug.LogError("<color=red>[Voice-6 ERROR] 스피커 주인을 찾을 수 없습니다!</color>");
        //    return;
        //}

        //string targetNick = remotePlayer.NickName.Replace("\0", "").Trim();
        //speaker.gameObject.name = $"VoiceSpeaker_{targetNick}";

        //Debug.Log($"<color=#FFAA00>[Voice-6] 상대방({targetNick})의 음성 스트림 도착! 매핑 코루틴 시작!</color>");

        //StartCoroutine(AttachSpeakerToAvatar(speaker, targetNick));

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

        //var remoteVoice = speaker.RemoteVoice;
        //// 💡 [수정] 즉시 실행하지 않고, 아주 짧은 프레임 대기 후 코루틴 시작
        //StartCoroutine(WaitForNickNameAndAttach(speaker, remoteVoice.PlayerId));


    }

    //IEnumerator WaitForNickNameAndAttach(Speaker speaker, int playerId)
    //{
    //    string targetNick = "";
    //    int retries = 0;
    //    const int MAX_RETRIES = 20; // 약 10초 대기

    //    Debug.Log($"<color=#FFAA00>[Voice] ID {playerId}의 닉네임 대기 시작...</color>");

    //    while (retries < MAX_RETRIES)
    //    {
    //        // 룸에 있는 플레이어 리스트에서 직접 탐색
    //        if (globalVoiceClient.Client.InRoom)
    //        {
    //            var player = globalVoiceClient.Client.CurrentRoom.GetPlayer(playerId);
    //            if (player != null && !string.IsNullOrEmpty(player.NickName))
    //            {
    //                targetNick = player.NickName.Replace("\0", "").Trim();
    //                if (targetNick != "") break; // 유효한 닉네임 확보 시 탈출
    //            }
    //        }

    //        retries++;
    //        yield return new WaitForSeconds(0.5f);
    //    }

    //    if (string.IsNullOrEmpty(targetNick))
    //    {
    //        // 💡 [백업 플랜] 닉네임 확보 실패 시 ID라도 사용하여 매핑 시도
    //        Debug.LogWarning($"<color=red>[Voice] {playerId}번 유저의 닉네임 확보 실패. ID로 매핑을 시도합니다.</color>");
    //        // 닉네임 대신 ID를 사용하여 Speaker 이름을 설정 (AttachSpeakerToAvatar에서도 ID 체크 로직 필요)
    //        targetNick = $"ID_{playerId}";
    //    }

    //    speaker.gameObject.name = $"VoiceSpeaker_{targetNick}";
    //    Debug.Log($"<color=#00FF00>[Voice-6 SUCCESS] {targetNick} 정보 매칭 완료!</color>");

    //    StartCoroutine(AttachSpeakerToAvatar(speaker, targetNick));
    //}

    IEnumerator AttachSpeakerToAvatar(Speaker speaker, string targetNick)
    {
        speaker.transform.SetParent(this.transform);

        string searchNick = targetNick.Replace("\0", "").Trim().ToLower();
        string simpleNick = searchNick.Split('#')[0];

        Transform targetAvatar = null;
        AudioSource aud = speaker.GetComponent<AudioSource>();

        // 💡 [핵심 1] 스피커 초기화 시 확실한 3D 세팅 및 범위 대폭 상향
        if (aud != null)
        {
            aud.rolloffMode = AudioRolloffMode.Linear; // 거리에 비례해 일정하게 소리 감소 (필수)
            aud.minDistance = 2f;
            aud.maxDistance = 50f; // 들리는 범위를 아주 넓게 설정하여 테스트
        }

        Debug.Log($"<color=#FFAA00>[Voice]</color> '{searchNick}' 아바타 추적 드론 가동 시작...");

        while (true)
        {
            if (speaker == null) yield break;

            // 타겟을 잃었거나 씬이 바뀌었을 때 (아직 아바타를 못 찾음)
            if (targetAvatar == null)
            {
                if (aud != null) aud.spatialBlend = 0f; // 2D 무전기 모드 (어디서든 들림)
                if (Camera.main != null) speaker.transform.position = Camera.main.transform.position;

                PlayerNameSync[] allSyncs = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);
                foreach (var sync in allSyncs)
                {
                    if (sync == null) continue;
                    string currentSyncNick = sync.NetworkNickname.Value.ToString().Replace("\0", "").Trim().ToLower();
                    string currentSimpleNick = currentSyncNick.Split('#')[0];

                    if (currentSyncNick == searchNick || (currentSimpleNick == simpleNick && !string.IsNullOrEmpty(simpleNick)))
                    {
                        targetAvatar = sync.transform;
                        Debug.Log($"<color=#00FF00>[Voice SUCCESS]</color> '{targetNick}' 새 아바타 발견! 3D 추적 재개.");
                        break;
                    }
                }

                if (targetAvatar == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
            }

            // 타겟 아바타를 찾고 씬에 존재할 때
            if (targetAvatar != null)
            {
                // 💡 [핵심 2] 씬 전환 등으로 인해 오디오가 멈췄다면 강제 재생
                if (aud != null && !aud.isPlaying)
                {
                    aud.Play();
                }

                // 다시 3D 생목소리 모드로 복구
                if (aud != null && aud.spatialBlend != 1.0f)
                {
                    aud.spatialBlend = 1.0f;
                }

                // 스피커 위치를 아바타 머리 위로 실시간 이동
                speaker.transform.position = targetAvatar.position + new Vector3(0, 1.8f, 0.2f);

                // 💡 [핵심 3] 내 카메라(귀)와 상대방 스피커 간의 실제 거리 로그 (약 5초마다 출력)
                if (Time.frameCount % 300 == 0 && Camera.main != null)
                {
                    float dist = Vector3.Distance(Camera.main.transform.position, speaker.transform.position);
                    Debug.Log($"<color=#00FFFF>[Voice Info]</color> 상대방 스피커와 내 카메라의 거리: {dist:F1}m");
                }

                yield return null;
            }
        }
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
    public void OnCreateRoomFailed(short returnCode, string message) { isConnecting = false; Debug.LogError($"[Voice] 방 생성 실패: {message}"); }
    public void OnJoinRoomFailed(short returnCode, string message) { isConnecting = false; Debug.LogError($"[Voice] 방 입장 실패: {message}"); }
    public void OnJoinRandomFailed(short returnCode, string message) { isConnecting = false; }
    public void OnLeftRoom() { isConnecting = false; }

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

        isConnecting = true;
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