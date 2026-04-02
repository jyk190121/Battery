using UnityEngine;
using Photon.Voice.Unity;
using Photon.Realtime;
using System.Collections.Generic;

public class VoiceRoomManager : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
{
    public UnityVoiceClient voiceClient;
    public string voiceRoomName = "GlobalPhoneRoom"; // 모든 플레이어가 대기할 보이스 전용 방

    private void Start()
    {
        if (voiceClient == null) voiceClient = GetComponent<UnityVoiceClient>();

        // 포톤 서버 이벤트(접속, 방 입장 등)를 이 스크립트가 수신하도록 등록
        voiceClient.Client.AddCallbackTarget(this);

        // 게임이 시작되면 보이스 서버 접속 시작!
        Debug.Log("[Voice] 보이스 서버에 접속을 시도합니다...");
        voiceClient.ConnectUsingSettings();
    }

    private void OnDestroy()
    {
        if (voiceClient != null) voiceClient.Client.RemoveCallbackTarget(this);
    }

    // 1. 마스터 서버 접속 성공 시 -> 방(전화망)에 입장 시도
    public void OnConnectedToMaster()
    {
        Debug.Log("[Voice] 마스터 서버 접속 성공! 통신망(방)에 입장합니다.");
        voiceClient.Client.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = voiceRoomName });
    }

    // 2. 방 입장 완료 시 -> 이제 진짜 통화 준비 완료!
    public void OnJoinedRoom()
    {
        Debug.Log($"[Voice] 통신망({voiceRoomName}) 입장 완료! 이제 통화 시 V키로 목소리 전송이 가능합니다.");
    }

    // ========================================================================
    // 아래는 인터페이스(IConnectionCallbacks, IMatchmakingCallbacks) 필수 구현용 빈 함수들입니다.
    // ========================================================================
    public void OnConnected() { }
    public void OnDisconnected(DisconnectCause cause) { Debug.Log($"[Voice] 연결 끊김: {cause}"); }
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