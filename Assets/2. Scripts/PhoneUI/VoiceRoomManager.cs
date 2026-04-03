using UnityEngine;
using Photon.Voice.Unity;
using Photon.Realtime;

public class VoiceRoomManager : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
{
    public static VoiceRoomManager Instance; // 외부에서 방 입장을 지시하기 위한 싱글톤
    public UnityVoiceClient voiceClient;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (voiceClient == null) voiceClient = GetComponent<UnityVoiceClient>();

        voiceClient.Client.AddCallbackTarget(this);

        // 게임 시작 시 방에 들어가지 않고, '마스터 서버'에 접속만 해두고 대기합니다.
        Debug.Log("[Voice] 보이스 서버에 접속을 시도합니다...");
        voiceClient.ConnectUsingSettings();
    }

    private void OnDestroy()
    {
        if (voiceClient != null) voiceClient.Client.RemoveCallbackTarget(this);
    }

    // [핵심 1] 1:1 비밀 통화방 입장 (A와 B의 닉네임을 조합하여 방 이름 생성)
    public void JoinCallRoom(string userA, string userB)
    {
        // 누가 먼저 걸었든 항상 똑같은 방 이름(알파벳 순)이 나오도록 정렬하여 입장
        string roomName = string.Compare(userA, userB) < 0 ? $"Call_{userA}_{userB}" : $"Call_{userB}_{userA}";
        Debug.Log($"[Voice] 1:1 통화방({roomName})에 입장합니다!");

        voiceClient.Client.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = roomName });
    }

    // [핵심 1] 통화 종료 시 방에서 퇴장
    public void LeaveCallRoom()
    {
        if (voiceClient != null && voiceClient.Client.InRoom)
        {
            Debug.Log("[Voice] 통화가 종료되어 방에서 퇴장합니다.");
            voiceClient.Client.OpLeaveRoom(false);
        }
    }

    public void OnConnectedToMaster()
    {
        Debug.Log("[Voice] 마스터 서버 접속 성공! 통화 대기 상태입니다.");
    }

    public void OnJoinedRoom()
    {
        Debug.Log("[Voice] 1:1 보이스 룸 입장 완료! V키로 통신이 가능합니다.");
    }

    // ========================================================================
    // 필수 구현 빈 함수들
    // ========================================================================
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