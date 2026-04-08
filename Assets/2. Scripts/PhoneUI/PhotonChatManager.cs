using UnityEngine;
using Photon.Chat;
using ExitGames.Client.Photon;
using System;
using TMPro;

public class PhotonChatManager : MonoBehaviour, IChatClientListener
{
    //[Header("Photon Settings")]
    public string chatAppId = "YOUR_APP_ID";
    public string userName;

    [Header("References")]
    public TeamChatRoomUI teamChatRoom;

    // [추가] 연결 상태 표시 UI
    public GameObject ifConnected;
    public GameObject connectYet;

    private ChatClient chatClient;
    private readonly string teamChannelName = "TeamRoomChannel";

    // 통화 이벤트
    public static event Action<string> OnIncomingCallReceived;
    public static event Action<string> OnCallAccepted;
    public static event Action<string> OnCallHungUp;
    public static event Action<string> OnCallBusy;

    public TextMeshProUGUI playerText;

    public bool CanChat => chatClient != null && chatClient.CanChat;

    private void Start()
    {
        if(MultiPlayerSessionManager.Instance != null)
        {
            userName = MultiPlayerSessionManager.Instance.PlayerNickname;
        }
        else
        {
            userName = "Guest";
        }

        chatClient = new ChatClient(this);
        chatClient.Connect(chatAppId, "1.0", new AuthenticationValues(userName));
        playerText.text = $"Player: {userName}";
    }

    private void Update()
    {
        if (chatClient != null) chatClient.Service();

        // 연결 상태에 따른 UI 활성화/비활성화
        if (ifConnected != null && connectYet != null)
        {
            ifConnected.SetActive(CanChat);
            connectYet.SetActive(!CanChat);
        }
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

                if (!isMine)
                {
                    bool isPhoneDownOrChatClosed = false;

                    if (PhoneUIController.Instance != null)
                    {
                        // 폰이 내려가 있거나, 채팅 화면이 꺼져 있는지 판단
                        isPhoneDownOrChatClosed = !PhoneUIController.Instance.phoneUIParent.activeSelf || !teamChatRoom.gameObject.activeInHierarchy;

                        if (isPhoneDownOrChatClosed)
                        {
                            if (PhoneUIController.Instance.messageNotificationObj != null)
                            {
                                PhoneUIController.Instance.messageNotificationObj.SetActive(true);
                            }

                            if (PhoneUIController.Instance.messageNotificationMobile != null)
                            {
                                PhoneUIController.Instance.messageNotificationMobile.SetActive(true);
                            }
                        }
                    }

                    // 사운드 재생 분기
                    if (isPhoneDownOrChatClosed)
                    {
                        // 폰을 내렸거나 다른 화면을 보고 있을 때
                        SoundManager.Instance.PlaySfx(SfxSound.PHONE_MESSAGE_ALARM);
                    }
                    else
                    {
                        // 폰을 들고 있고, 채팅방을 보고 있을 때
                        SoundManager.Instance.PlaySfx(SfxSound.PHONE_MESSAGE_RECEIVE);
                    }
                }
            }
        }
    }

    public void OnPrivateMessage(string sender, object message, string channelName)
    {
        if (sender == userName) return;

        string msgText = message.ToString();

        if (msgText == "CALL_REQUEST")
        {
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
        else if (msgText == "CALL_BUSY")
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