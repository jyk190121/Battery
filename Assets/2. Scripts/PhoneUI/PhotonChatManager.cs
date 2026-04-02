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

    // 통화 수락 이벤트
    public static event Action<string> OnIncomingCallReceived;
    public static event Action<string> OnCallAccepted;
    public static event Action<string> OnCallHungUp;

    // 테스트 
    public TextMeshProUGUI playerText;

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
        if (chatClient != null && chatClient.CanChat)
            chatClient.PublishMessage(teamChannelName, message);
    }

    #region [전화 시스템] 개인 메시지를 활용한 신호 전송
    public void SendCallRequest(string targetPlayerName)
    {
        if (chatClient != null && chatClient.CanChat)
        {
            chatClient.SendPrivateMessage(targetPlayerName, "CALL_REQUEST");
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = true;
        }
    }

    // 수락 신호 보내기
    public void SendCallAccept(string targetPlayerName)
    {
        if (chatClient != null && chatClient.CanChat)
            chatClient.SendPrivateMessage(targetPlayerName, "CALL_ACCEPT");
    }

    public void SendCallHangUp(string targetPlayerName)
    {
        if (chatClient != null && chatClient.CanChat)
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
            // 통화 중복 방지 (이미 전화 중이면 무시)
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.isCallActive) return;

            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = true;
            OnIncomingCallReceived?.Invoke(sender);
        }
        else if (msgText == "CALL_ACCEPT") // [추가] 상대방이 전화를 받음!
        {
            OnCallAccepted?.Invoke(sender);
        }
        else if (msgText == "CALL_HANGUP")
        {
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
            OnCallHungUp?.Invoke(sender);
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