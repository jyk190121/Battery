using UnityEngine;
using Photon.Chat;
using ExitGames.Client.Photon;
using System; // Action 사용을 위해 추가

public class PhotonChatManager : MonoBehaviour, IChatClientListener
{
    [Header("Photon Settings")]
    public string chatAppId = "YOUR_APP_ID";
    public string userName = "Player1";

    [Header("References")]
    public TeamChatRoomUI teamChatRoom;

    private ChatClient chatClient;
    private readonly string teamChannelName = "TeamRoomChannel";

    // [전화 시스템 전용 이벤트] 누군가 나에게 전화를 걸거나 끊었을 때 발송
    public static event Action<string> OnIncomingCallReceived;
    public static event Action<string> OnCallHungUp;

    private void Awake()
    {
        chatClient = new ChatClient(this);
        chatClient.Connect(chatAppId, "1.0", new AuthenticationValues(userName));
        Debug.Log($"[PhotonChat] {userName} 닉네임으로 접속 시도 중...");
    }

    private void Update()
    {
        if (chatClient != null) chatClient.Service();
    }

    public void SendChatMessage(string message)
    {
        if (chatClient != null && chatClient.CanChat)
        {
            chatClient.PublishMessage(teamChannelName, message);
        }
    }

    #region [전화 시스템] 개인 메시지를 활용한 신호 전송
    // 1. 특정 플레이어에게 전화 걸기 신호 쏘기
    public void SendCallRequest(string targetPlayerName)
    {
        if (chatClient != null && chatClient.CanChat)
        {
            chatClient.SendPrivateMessage(targetPlayerName, "CALL_REQUEST");
            Debug.Log($"[Phone] {targetPlayerName}에게 통화 연결을 시도합니다...");
        }
    }

    // 2. 통화 거절/종료 신호 쏘기
    public void SendCallHangUp(string targetPlayerName)
    {
        if (chatClient != null && chatClient.CanChat)
        {
            chatClient.SendPrivateMessage(targetPlayerName, "CALL_HANGUP");
            Debug.Log($"[Phone] {targetPlayerName}와의 통화를 종료/거절합니다.");
        }
    }
    #endregion

    #region IChatClientListener 필수 구현
    public void OnConnected()
    {
        Debug.Log("[PhotonChat] 서버 연결 성공! 팀 채널에 입장합니다.");
        chatClient.Subscribe(new string[] { teamChannelName });
    }

    public void OnGetMessages(string channelName, string[] senders, object[] messages)
    {
        if (channelName == teamChannelName && teamChatRoom != null)
        {
            for (int i = 0; i < senders.Length; i++)
            {
                bool isMine = (senders[i] == userName);
                teamChatRoom.ReceiveMessage(senders[i], messages[i].ToString(), isMine);
            }
        }
    }

    // [핵심] 누군가 나에게 개인 메시지(전화 신호)를 보냈을 때 감지하는 곳
    public void OnPrivateMessage(string sender, object message, string channelName)
    {
        // 내가 나한테 보낸 메시지는 무시
        if (sender == userName) return;

        string msgText = message.ToString();

        if (msgText == "CALL_REQUEST")
        {
            Debug.Log($"[Phone] 따르릉! {sender}에게서 전화가 왔습니다!");

            // 사령탑에 상태 기록 (유저님이 나중에 UI/소리 띄울 때 쓸 변수)
            if (PhoneUIController.Instance != null)
            {
                PhoneUIController.Instance.isReceivingCall = true;
                PhoneUIController.Instance.currentCallerName = sender;
            }

            // 이벤트 발송 (폰 UI가 알아서 반응하도록)
            OnIncomingCallReceived?.Invoke(sender);
        }
        else if (msgText == "CALL_HANGUP")
        {
            Debug.Log($"[Phone] 뚜-뚜- {sender}가 전화를 끊었습니다.");

            if (PhoneUIController.Instance != null)
            {
                PhoneUIController.Instance.isReceivingCall = false;
                PhoneUIController.Instance.currentCallerName = "";
            }

            OnCallHungUp?.Invoke(sender);
        }
    }

    public void DebugReturn(DebugLevel level, string message) { }
    public void OnDisconnected() { Debug.Log("[PhotonChat] 서버와 연결이 끊어졌습니다."); }
    public void OnChatStateChange(ChatState state) { }
    public void OnSubscribed(string[] channels, bool[] results) { }
    public void OnUnsubscribed(string[] channels) { }
    public void OnStatusUpdate(string user, int status, bool gotMessage, object message) { }
    public void OnUserSubscribed(string channel, string user) { }
    public void OnUserUnsubscribed(string channel, string user) { }
    #endregion
}