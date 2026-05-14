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
    private string currentRoomName = "";


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
        globalRecorder.TransmitEnabled = false;

        // [수정] 두 가지 콜백을 모두 등록합니다.
        globalVoiceClient.Client.AddCallbackTarget(this);
        globalVoiceClient.SpeakerLinked += OnSpeakerLinked;
    }

    private void Update()
    {
        if (globalVoiceClient != null)
        {
            // 연결 상태를 실시간으로 확인
            // Client.InRoom이 false라면 아무리 말해도 전달되지 않습니다.
            if (Time.frameCount % 60 == 0) // 매 초마다 출력
            {
                Debug.Log($"<color=white>[Voice State]</color> InRoom: {globalVoiceClient.Client.InRoom}, State: {globalVoiceClient.Client.State}");
            }
        }

        // 입력 시스템이나 리코더가 없거나, 룸에 입장하지 않은 상태면 아무 동작도 하지 않음
        if (Keyboard.current == null || globalRecorder == null || !globalVoiceClient.Client.InRoom)
        {
            // 만약 룸 밖인데 전송이 켜져있다면 강제로 꺼줍니다 (안전장치)
            if (globalRecorder != null && globalRecorder.TransmitEnabled)
            {
                globalRecorder.TransmitEnabled = false;
            }
            return;
        }

        // 1. UI 차단 상태 (전화 중이거나 메뉴를 열었을 때 등)
        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isInputBlocked)
        {
            // 전송 중이었다면 즉시 차단
            if (globalRecorder.TransmitEnabled)
            {
                globalRecorder.TransmitEnabled = false;
                Debug.Log("<color=orange>[Voice-5] UI 차단 상태: 마이크 송신 강제 종료</color>");
            }
            return; // V키 입력을 무시하고 리턴
        }

        // 2. Push-To-Talk (눌러서 말하기) 처리
        bool isVPressed = Keyboard.current.vKey.isPressed;

        // 상태가 변경될 때만 로그를 찍고 전송 상태를 업데이트 (최적화)
        if (globalRecorder.TransmitEnabled != isVPressed)
        {
            globalRecorder.TransmitEnabled = isVPressed;

            if (isVPressed)
                Debug.Log("<color=yellow>[Voice-5] V키 누름! 내 마이크 데이터를 서버로 쏘기 시작합니다!</color>");
            else
                Debug.Log("<color=grey>[Voice-5] V키 뗌! 마이크 송신 중지</color>");
        }
    }

    public void ConnectVoice(string myNickname, string roomName)
    {
        StopAllCoroutines();
        StartCoroutine(ReconnectVoiceRoutine(myNickname, roomName));
    }

    IEnumerator ReconnectVoiceRoutine(string myNickname, string roomName)
    {
        // 1. 기존 소켓이 살아있다면 확실히 연결 해제 대기
        if (globalVoiceClient.Client.IsConnected || globalVoiceClient.Client.State != ClientState.PeerCreated)
        {
            Debug.Log("[Voice] 기존 소켓 정리 대기...");
            globalVoiceClient.Client.Disconnect();

            float timeout = 3.0f;
            while (globalVoiceClient.Client.State != ClientState.Disconnected &&
                   globalVoiceClient.Client.State != ClientState.PeerCreated && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        // 2. 이전 씬의 스피커 잔해 제거
        Speaker[] activeSpeakers = GetComponentsInChildren<Speaker>();
        foreach (var s in activeSpeakers) Destroy(s.gameObject);

        // 3. 데이터 및 콜백 재설정
        currentRoomName = roomName;
        globalVoiceClient.Client.NickName = myNickname;

        // 4. 마이크 하드웨어 리셋 (데시벨 먹통 해결)
        if (globalRecorder != null)
        {
            globalRecorder.TransmitEnabled = false;
            globalRecorder.RecordingEnabled = false;
            yield return new WaitForSeconds(0.2f);
            globalRecorder.RecordingEnabled = true;
            globalRecorder.RestartRecording();
        }

        yield return new WaitForSeconds(0.5f);

        bool isConnecting = globalVoiceClient.ConnectUsingSettings();
        Debug.Log($"<color=#FF55FF>[Voice-1] 접속 시도 결과: {isConnecting}</color>");

        // 만약 이미 연결된 상태에서 이 루틴이 불렸다면 OnConnectedToMaster가 안 불릴 수 있으므로 체크
        if (globalVoiceClient.Client.State == ClientState.ConnectedToMasterServer)
        {
            JoinVoiceRoomInternal();
        }
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
        Debug.Log($"<color=#55FF55>[Voice-3] 보이스 방 입장 완벽 성공! 현재 방 인원: {globalVoiceClient.Client.CurrentRoom.PlayerCount}명</color>");
    }

    public void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"<color=red>[Voice Error] 방 입장 실패: {message}</color>");
    }

    public void OnDisconnected(DisconnectCause cause)
    {
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

        var remoteVoice = speaker.RemoteVoice;
        // 💡 [수정] 즉시 실행하지 않고, 아주 짧은 프레임 대기 후 코루틴 시작
        StartCoroutine(WaitForNickNameAndAttach(speaker, remoteVoice.PlayerId));
    }

    IEnumerator WaitForNickNameAndAttach(Speaker speaker, int playerId)
    {
        string targetNick = "";
        int retries = 0;
        const int MAX_RETRIES = 20; // 약 10초 대기

        Debug.Log($"<color=#FFAA00>[Voice] ID {playerId}의 닉네임 대기 시작...</color>");

        while (retries < MAX_RETRIES)
        {
            // 룸에 있는 플레이어 리스트에서 직접 탐색
            if (globalVoiceClient.Client.InRoom)
            {
                var player = globalVoiceClient.Client.CurrentRoom.GetPlayer(playerId);
                if (player != null && !string.IsNullOrEmpty(player.NickName))
                {
                    targetNick = player.NickName.Replace("\0", "").Trim();
                    if (targetNick != "") break; // 유효한 닉네임 확보 시 탈출
                }
            }

            retries++;
            yield return new WaitForSeconds(0.5f);
        }

        if (string.IsNullOrEmpty(targetNick))
        {
            // 💡 [백업 플랜] 닉네임 확보 실패 시 ID라도 사용하여 매핑 시도
            Debug.LogWarning($"<color=red>[Voice] {playerId}번 유저의 닉네임 확보 실패. ID로 매핑을 시도합니다.</color>");
            // 닉네임 대신 ID를 사용하여 Speaker 이름을 설정 (AttachSpeakerToAvatar에서도 ID 체크 로직 필요)
            targetNick = $"ID_{playerId}";
        }

        speaker.gameObject.name = $"VoiceSpeaker_{targetNick}";
        Debug.Log($"<color=#00FF00>[Voice-6 SUCCESS] {targetNick} 정보 매칭 완료!</color>");

        StartCoroutine(AttachSpeakerToAvatar(speaker, targetNick));
    }


    private IEnumerator AttachSpeakerToAvatar(Speaker speaker, string targetNick)
    {
        bool isAttached = false;
        float timeout = 120f;

        int fallbackId = -1;
        if (targetNick.StartsWith("ID_")) int.TryParse(targetNick.Replace("ID_", ""), out fallbackId);

        Debug.Log($"<color=#FFAA00>[Voice-6.5] {targetNick} 스피커 부착 코루틴 진입. 탐색을 시작합니다...</color>");

        while (!isAttached && timeout > 0)
        {
            if (speaker == null) yield break;

            foreach (var p in PlayerController.AllPlayers)
            {
                if (p == null || !p.IsSpawned) continue;

                if (p.TryGetComponent(out PlayerNameSync nameSync))
                {
                    string currentSyncNick = nameSync.NetworkNickname.Value.ToString().Replace("\0", "").Trim();

                    // 💡 [수정] 닉네임 매칭 혹은 Photon Player ID 매칭 시도
                    // PlayerNameSync에 OwnerClientId를 사용하여 비교하는 것이 가장 확실합니다.
                    bool isNameMatch = !string.IsNullOrEmpty(currentSyncNick) && currentSyncNick == targetNick;

                    // 닉네임 확보 실패 시 fallbackId(플레이어 번호)와 ClientId를 대조 (임시 방편)
                    bool isIdMatch = (fallbackId != -1 && (int)p.OwnerClientId + 1 == fallbackId);

                    if (isNameMatch || isIdMatch)
                    {
                        speaker.transform.SetParent(p.transform);
                        speaker.transform.localPosition = new Vector3(0, 1.8f, 0.2f);

                        // 💡 소리가 들리도록 AudioSource 강제 활성화
                        var aud = speaker.GetComponent<AudioSource>();
                        if (aud != null) aud.volume = 1.0f;

                        Debug.Log($"<color=#00FF00>[Voice-7 SUCCESS] {currentSyncNick} (ID:{p.OwnerClientId}) 아바타 매핑 완료!</color>");
                        isAttached = true;
                        break;
                    }
                }
            }

            if (!isAttached)
            {
                timeout -= 1.0f;
                yield return new WaitForSeconds(1.0f);
            }
        }

        if (!isAttached)
        {
            Debug.LogError($"<color=red>[Voice-7 FAIL] 120초 동안 {targetNick}을 찾지 못해 스피커 부착에 실패했습니다.</color>");
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

    // 빈 인터페이스 구현부들
    public void OnConnected() { }
    public void OnRegionListReceived(RegionHandler regionHandler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string debugMessage) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { }

    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    #region 씬이동 시 참조 파괴 방지 스크립트

    // 마스터 서버 연결 완료 시 호출되는 콜백
    public void OnConnectedToMaster()
    {
        Debug.Log("<color=cyan>[Voice]</color> 마스터 서버 연결 완료. 방 입장을 시도합니다.");
        JoinVoiceRoomInternal();
    }

    // 실제 방 입장 로직을 별도 함수로 분리
    void JoinVoiceRoomInternal()
    {
        // 💡 룸에 없고, 방 이름이 있을 때만 입장 요청
        if (globalVoiceClient != null && !globalVoiceClient.Client.InRoom && !string.IsNullOrEmpty(currentRoomName))
        {
            EnterRoomParams enterParams = new EnterRoomParams
            {
                RoomName = currentRoomName,
                RoomOptions = new RoomOptions { MaxPlayers = 4 },
                Lobby = TypedLobby.Default
            };

            bool sent = globalVoiceClient.Client.OpJoinOrCreateRoom(enterParams);
            Debug.Log($"<color=cyan>[Voice]</color> {currentRoomName} 방 입장 요청 송신 결과: {sent}");
        }
    }

    #endregion
}