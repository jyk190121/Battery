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
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
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
        if (Keyboard.current == null || globalRecorder == null || !globalVoiceClient.Client.InRoom) return;

        if (PhoneUIController.Instance != null && PhoneUIController.Instance.isInputBlocked)
        {
            if (globalRecorder.TransmitEnabled)
            {
                globalRecorder.TransmitEnabled = false;
                Debug.Log("<color=orange>[Voice-5] UI 차단 상태: 마이크 송신 강제 종료</color>");
            }
            return;
        }

        bool isVPressed = Keyboard.current.vKey.isPressed;
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
        currentRoomName = roomName;
        globalVoiceClient.Client.NickName = myNickname;
        bool isConnecting = globalVoiceClient.ConnectUsingSettings();
        Debug.Log($"<color=#FF55FF>[Voice-1] Netcode로부터 연결 요청 받음! (닉네임: {myNickname}, 방: {roomName}) / 결과: {isConnecting}</color>");
    }

    public void OnConnectedToMaster()
    {
        Debug.Log($"<color=#55FFFF>[Voice-2] 포톤 마스터 서버 접속 성공! 즉시 방({currentRoomName}) 입장을 시도합니다.</color>");
        globalVoiceClient.Client.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = currentRoomName });
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
        Debug.LogError($"<color=red>[Voice Error] 보이스 서버 연결 끊김! 원인: {cause}</color>");
    }

    private void OnSpeakerLinked(Speaker speaker)
    {
        Photon.Realtime.Player remotePlayer = globalVoiceClient.Client.CurrentRoom.GetPlayer(speaker.RemoteVoice.PlayerId);
        if (remotePlayer == null)
        {
            Debug.LogError("<color=red>[Voice-6 ERROR] 스피커 주인을 찾을 수 없습니다!</color>");
            return;
        }

        string targetNick = remotePlayer.NickName.Replace("\0", "").Trim();
        speaker.gameObject.name = $"VoiceSpeaker_{targetNick}";

        Debug.Log($"<color=#FFAA00>[Voice-6] 상대방({targetNick})의 음성 스트림 도착! 매핑 코루틴 시작!</color>");

        StartCoroutine(AttachSpeakerToAvatar(speaker, targetNick));
    }

    private IEnumerator AttachSpeakerToAvatar(Speaker speaker, string targetNick)
    {
        bool isAttached = false;
        int retries = 0;

        while (!isAttached && retries < 40)
        {
            PlayerNameSync[] allPlayers = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p.NetworkNickname.Value.ToString().Replace("\0", "").Trim() == targetNick)
                {
                    speaker.transform.SetParent(p.transform);
                    speaker.transform.localPosition = new Vector3(0, 1.8f, 0.2f);

                    Debug.Log($"<color=#00FF00>[Voice-7 SUCCESS] {targetNick} 아바타에 스피커 부착을 완료했습니다!</color>");
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
    }

    public void SetCallMode(string targetNickname, bool isCalling)
    {
        VoiceController[] controllers = FindObjectsByType<VoiceController>(FindObjectsSortMode.None);
        foreach (var vc in controllers)
        {
            if (vc.gameObject.name == $"VoiceSpeaker_{targetNickname}")
            {
                vc.SetCallMode(isCalling);
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
}