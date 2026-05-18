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
        if(Microphone.devices.Length > 0)
        {
            globalRecorder.MicrophoneDevice = new Photon.Voice.DeviceInfo(Microphone.devices[0]);
        }


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

        currentRoomName = roomName;
        globalVoiceClient.Client.NickName = nickname;

        if(globalVoiceClient.Client.State == ClientState.PeerCreated ||
            globalVoiceClient.Client.State == ClientState.Disconnected)
        {
            print("보이스 서버 신규접속 시도");
            globalVoiceClient.ConnectUsingSettings();
        }

        else if(globalVoiceClient.Client.Server == ServerConnection.MasterServer)
        {
            JoinVoiceRoomInternal();
        }

    }
   
    public void ShutdownVoice()
    {
        if (globalVoiceClient != null)
        {
            globalRecorder.RecordingEnabled = false;
            globalRecorder.TransmitEnabled = false;
        }

        if (globalVoiceClient.Client.IsConnected)
        {
            globalVoiceClient.Client.Disconnect();
        }

        currentRoomName = "";
        Debug.Log("<color=red>[Voice]</color> 시스템 셧다운 및 마이크 해제 완료");
    }

    // ==========================================
    // [관문 3] 드디어 작동할 방 입장 콜백!
    // ==========================================
    public void OnJoinedRoom()
    {
        Debug.Log($"<color=#55FF55>[Voice-3]</color> 보이스 방 입장 성공! ({globalVoiceClient.Client.CurrentRoom.Name})");
    }
    public void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"<color=red>[Voice Error] 보이스 서버 연결 끊김! 원인: 방 파괴");
    }

    private void OnSpeakerLinked(Speaker speaker)
    {

        //int playerId = speaker.RemoteVoice.PlayerId;
        //speaker.gameObject.name = $"Global_Speaker_{playerId}";

        //AudioSource aud = speaker.GetComponent<AudioSource>();
        //if (aud != null)
        //{
        //    aud.spatialBlend = 0f;  // 2D 사운드
        //    aud.volume = 1f;
        //    aud.mute = false;
        //    aud.playOnAwake = true;
        //}

        //print($"상대방 Player {playerId} 의 스피커 셋팅 완료");

        int playerId = speaker.RemoteVoice.PlayerId;
        speaker.gameObject.name = $"Global_Speaker_{playerId}";

        AudioSource aud = speaker.GetComponent<AudioSource>();
        if (aud != null)
        {
            // 💡 1. 3D 사운드로 변경
            aud.spatialBlend = 1.0f;
            aud.rolloffMode = AudioRolloffMode.Linear;
            aud.minDistance = 2f;
            aud.maxDistance = 25f; // 소리가 들리는 최대 거리
            aud.playOnAwake = true;
        }

        // 💡 2. 스피커를 허공(0,0,0)에 두지 않고, 상대방 캐릭터를 찾아서 머리통에 붙여줍니다!
        StartCoroutine(AttachSpeakerToPlayer(speaker, playerId));
    }

    IEnumerator AttachSpeakerToPlayer(Speaker speaker, int playerId)
    {
        // 1. 캐릭터가 스폰되고 닉네임이 동기화될 때까지 약간 대기 (필수)
        yield return new WaitForSeconds(1.5f);

        if (speaker == null) yield break;

        // 2. 포톤 서버에서 현재 스피커의 주인(playerId)이 누구인지 닉네임을 가져옵니다.
        string photonNickname = "";
        var currentRoom = globalVoiceClient.Client.CurrentRoom;

        if (currentRoom != null)
        {
            Photon.Realtime.Player photonPlayer = currentRoom.GetPlayer(playerId);
            if (photonPlayer != null)
            {
                photonNickname = photonPlayer.NickName;
            }
        }

        if (string.IsNullOrEmpty(photonNickname))
        {
            Debug.LogWarning($"<color=red>[Voice]</color> 포톤 플레이어(ID:{playerId})의 닉네임을 찾을 수 없습니다.");
            yield break;
        }

        // 3. 씬에 있는 모든 PlayerNameSync (네트워크 캐릭터)를 찾습니다.
        PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

        foreach (var p in allPlayers)
        {
            // 4. Netcode의 닉네임과 Photon의 닉네임을 비교합니다!
            string netcodeNickname = p.NetworkNickname.Value.ToString().Replace("\0", "").Trim();

            if (netcodeNickname == photonNickname)
            {
                // 💡 일치하는 캐릭터를 찾았습니다! 스피커를 캐릭터의 자식(Child)으로 넣습니다.
                speaker.transform.SetParent(p.transform);

                // 💡 목소리가 발밑이 아니라 '머리(입)' 위치에서 나도록 Y축을 1.5f 정도 올려줍니다.
                speaker.transform.localPosition = new Vector3(0, 1.5f, 0);

                Debug.Log($"<color=magenta>[Voice]</color> 3D 스피커를 '{photonNickname}' 캐릭터에 완벽하게 부착했습니다!");
                break;
            }
        }
    }
    /// <summary>
    /// 스마트폰 통화 상태에 따라 특정 플레이어의 목소리를 2D(통화) 또는 3D(일반)로 전환합니다.
    /// </summary>
    /// <param name="targetNickname">통화 대상 닉네임</param>
    /// <param name="isCallActive">true면 2D 통화 모드, false면 3D 일반 대화 모드</param>
    public void SetCallMode(string targetNickname, bool isCallActive)
    {
        // 1. 현재 접속 중인 방에서 상대방(targetNickname)의 고유 ID(PlayerId)를 찾습니다.
        int targetPlayerId = -1;
        var currentRoom = globalVoiceClient.Client.CurrentRoom;

        if (currentRoom != null)
        {
            foreach (var p in currentRoom.Players.Values)
            {
                if (p.NickName == targetNickname)
                {
                    targetPlayerId = p.ActorNumber;
                    break;
                }
            }
        }

        if (targetPlayerId == -1)
        {
            Debug.LogWarning($"<color=red>[Phone]</color> '{targetNickname}' 플레이어를 방에서 찾을 수 없습니다.");
            return;
        }

        // 2. 씬에 존재하는 모든 포톤 스피커 중, 상대방의 스피커를 찾아 2D/3D 전환!
        Photon.Voice.Unity.Speaker[] allSpeakers = FindObjectsByType<Photon.Voice.Unity.Speaker>(FindObjectsSortMode.None);

        foreach (var speaker in allSpeakers)
        {
            if (speaker.RemoteVoice.PlayerId == targetPlayerId)
            {
                AudioSource aud = speaker.GetComponent<AudioSource>();
                if (aud != null)
                {
                    if (isCallActive)
                    {
                        // 📞 통화 모드 (2D 귀에 직접 꽂힘)
                        aud.spatialBlend = 0f;
                        aud.bypassEffects = true; // 거리나 공간 이펙트 무시
                        Debug.Log($"<color=magenta>[Phone]</color> '{targetNickname}'와의 통화 연결! 사운드를 2D로 전환합니다.");
                    }
                    else
                    {
                        // 🗣️ 일반 모드 (3D 캐릭터 위치 기반)
                        aud.spatialBlend = 1.0f;
                        aud.bypassEffects = false;
                        Debug.Log($"<color=magenta>[Phone]</color> '{targetNickname}'와의 통화 종료! 사운드를 3D로 복구합니다.");
                    }
                }
                break;
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
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { }

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