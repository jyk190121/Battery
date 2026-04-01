using UnityEngine;
using Photon.Chat;
using ExitGames.Client.Photon;

public class PhotonChatManager : MonoBehaviour, IChatClientListener
{
    [Header("Photon Settings")]
    [Tooltip("Photon 홈페이지에서 발급받은 Chat App ID를 입력하세요.")]
    public string chatAppId = "YOUR_APP_ID";
    [Tooltip("현재 플레이어의 닉네임 (멀티 테스트 시 서로 다르게 설정해야 합니다)")]
    public string userName = "Player1";

    [Header("References")]
    public TeamChatRoomUI teamChatRoom; // TeamRoom UI 스크립트 연결

    private ChatClient chatClient;
    private readonly string teamChannelName = "TeamRoomChannel"; // 4명이 모일 채널 이름

    private void Start()
    {
        // 포톤 채팅 서버 연결 시도
        chatClient = new ChatClient(this);
        chatClient.Connect(chatAppId, "1.0", new AuthenticationValues(userName));
        Debug.Log($"[PhotonChat] {userName} 닉네임으로 접속 시도 중...");
    }

    private void Update()
    {
        // 서버와의 통신을 유지하기 위해 매 프레임 반드시 호출
        if (chatClient != null)
        {
            chatClient.Service();
        }
    }

    // TeamChatRoomUI에서 메시지를 보낼 때 호출하는 함수
    public void SendChatMessage(string message)
    {
        if (chatClient != null && chatClient.CanChat)
        {
            chatClient.PublishMessage(teamChannelName, message);
        }
    }

    #region IChatClientListener 필수 구현 (서버 응답 수신)

    public void OnConnected()
    {
        Debug.Log("[PhotonChat] 서버 연결 성공! 팀 채널에 입장합니다.");
        // 연결 성공 시 지정된 채널(방)에 입장
        chatClient.Subscribe(new string[] { teamChannelName });
    }

    // 서버로부터 메시지가 도착했을 때 실행
    public void OnGetMessages(string channelName, string[] senders, object[] messages)
    {
        if (channelName == teamChannelName && teamChatRoom != null)
        {
            for (int i = 0; i < senders.Length; i++)
            {
                // 내가 보낸 메시지인지 확인하여 말풍선 방향을 결정
                bool isMine = (senders[i] == userName);
                string messageText = messages[i].ToString();

                // 닉네임과 함께 메시지 전달 (예: "Player2: 안녕하세요")
                teamChatRoom.ReceiveMessage(senders[i], messageText, isMine);
            }
        }
    }

    // 미사용 필수 인터페이스
    public void DebugReturn(DebugLevel level, string message) { }
    public void OnDisconnected() { Debug.Log("[PhotonChat] 서버와 연결이 끊어졌습니다."); }
    public void OnChatStateChange(ChatState state) { }
    public void OnSubscribed(string[] channels, bool[] results) { Debug.Log($"[PhotonChat] {channels[0]} 입장 완료 (최대 4인)"); }
    public void OnUnsubscribed(string[] channels) { }
    public void OnPrivateMessage(string sender, object message, string channelName) { }
    public void OnStatusUpdate(string user, int status, bool gotMessage, object message) { }
    public void OnUserSubscribed(string channel, string user) { }
    public void OnUserUnsubscribed(string channel, string user) { }

    #endregion
}