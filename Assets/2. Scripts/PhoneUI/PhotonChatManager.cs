using UnityEngine;
using Photon.Chat;
using ExitGames.Client.Photon;
using System;
using TMPro;

public class PhotonChatManager : MonoBehaviour, IChatClientListener
{
    [Header("Photon Settings")]
    public string chatAppId = "YOUR_APP_ID";
    public string userName = "Player1";

    [Header("References")]
    public TeamChatRoomUI teamChatRoom;

    private ChatClient chatClient;
    private readonly string teamChannelName = "TeamRoomChannel";

    // 통화 이벤트
    public static event Action<string> OnIncomingCallReceived;
    public static event Action<string> OnCallAccepted;
    public static event Action<string> OnCallHungUp;
    public static event Action<string> OnCallBusy; // [추가] 통화 중 거절 이벤트

    // 테스트 
    public TextMeshProUGUI playerText;

    // [핵심 3] 외부에서 현재 서버에 완전히 접속되었는지 확인하기 위한 프로퍼티
    public bool CanChat => chatClient != null && chatClient.CanChat;

    private void Awake()
    {
        chatClient = new ChatClient(this);
        chatClient.Connect(chatAppId, "1.0", new AuthenticationValues(userName));
        playerText.text = $"Player: {userName}";
    }

    private void Update()
    {
        if (chatClient != null) chatClient.Service();
    }

    public void SendChatMessage(string message)
    {
        if (CanChat) chatClient.PublishMessage(teamChannelName, message);
    }

    #region [전화 시스템] 개인 메시지를 활용한 신호 전송
    public void SendCallRequest(string targetPlayerName)
    {
        if (CanChat)
        {
            chatClient.SendPrivateMessage(targetPlayerName, "CALL_REQUEST");
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = true;
        }
    }

    public void SendCallAccept(string targetPlayerName)
    {
        if (CanChat) chatClient.SendPrivateMessage(targetPlayerName, "CALL_ACCEPT");
    }

    public void SendCallHangUp(string targetPlayerName)
    {
        if (CanChat)
        {
            chatClient.SendPrivateMessage(targetPlayerName, "CALL_HANGUP");
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
        }
    }
    #endregion

    #region IChatClientListener 필수 구현
    public void OnConnected()
    {
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

    public void OnPrivateMessage(string sender, object message, string channelName)
    {
        if (sender == userName) return;

        string msgText = message.ToString();

        if (msgText == "CALL_REQUEST")
        {
            // [핵심 2] 내가 통화 중이라면 무시하지 않고 거절 신호(BUSY)를 보냄!
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.isCallActive)
            {
                chatClient.SendPrivateMessage(sender, "CALL_BUSY");
                return;
            }

            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = true;
            OnIncomingCallReceived?.Invoke(sender);
        }
        else if (msgText == "CALL_ACCEPT")
        {
            OnCallAccepted?.Invoke(sender);
        }
        else if (msgText == "CALL_HANGUP")
        {
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
            OnCallHungUp?.Invoke(sender);
        }
        else if (msgText == "CALL_BUSY") // [추가] 상대가 통화 중일 때 받는 신호
        {
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
            OnCallBusy?.Invoke(sender);
        }
    }

    public void DebugReturn(DebugLevel level, string message) { }
    public void OnDisconnected() { }
    public void OnChatStateChange(ChatState state) { }
    public void OnSubscribed(string[] channels, bool[] results) { }
    public void OnUnsubscribed(string[] channels) { }
    public void OnStatusUpdate(string user, int status, bool gotMessage, object message) { }
    public void OnUserSubscribed(string channel, string user) { }
    public void OnUserUnsubscribed(string channel, string user) { }
    #endregion
}