using UnityEngine;
using Photon.Chat;
using ExitGames.Client.Photon;
using System;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

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
    private string teamChannelName = "GlobalChannel";

    // 통화 이벤트
    public static event Action<string> OnIncomingCallReceived;
    public static event Action<string> OnCallAccepted;
    public static event Action<string> OnCallHungUp;
    public static event Action<string> OnCallBusy;

    //public TextMeshProUGUI playerText;

    public bool CanChat => chatClient != null && chatClient.CanChat;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (SceneManager.GetActiveScene().name == "KJY_Lobby")
        {
            SetUp();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 로비 씬으로 넘어와서 방 접속이 확정되었을 때 챗 서버 연결!
        if (scene.name == "KJY_Lobby")
        {
            SetUp();
        }
    }

    public void SetUp()
    {
        if (MultiPlayerSessionManager.Instance == null) return;

        // 1. 목표 채널 이름 파악 
        // MultiPlayerSessionManager에 저장된 확실한 문자열만
        string targetChannel = MultiPlayerSessionManager.Instance.CurrentChannelId;

        // 2. 이미 포톤 챗에 접속되어 있는 상태라면 스킵
        if (chatClient != null && chatClient.State == ChatState.ConnectedToFrontEnd)
        {
            if (teamChannelName == targetChannel) return;
            else chatClient.Disconnect();
        }

        // 3. 내 고유 닉네임과 채널 이름 세팅
        userName = MultiPlayerSessionManager.Instance.PlayerNickname;
        teamChannelName = targetChannel;

        chatClient = new ChatClient(this);

        chatClient.ChatRegion = "asia";
        bool isSuccess = chatClient.Connect(chatAppId, "1.0", new AuthenticationValues(userName));
        if (!isSuccess)
        {
            Debug.LogError("[PhotonChat] 연결 시도 즉시 실패! App ID나 인터넷 상태를 확인하세요.");
        }

        Debug.Log($"[PhotonChat] 챗 서버 접속 시도! (닉네임: {userName} / 방코드: {teamChannelName})");
    }

    private void Update()
    {
        if (chatClient != null) chatClient.Service();

        if (Keyboard.current.f12Key.wasReleasedThisFrame)
        {
            SetUp();
        }

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
        Debug.Log("[PhotonChat] 챗 서버 접속 완벽하게 성공! 채널 구독 시도...");
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

    public void DebugReturn(DebugLevel level, string message)
    {
        if (level == DebugLevel.ERROR || level == DebugLevel.WARNING)
            Debug.LogWarning($"[PhotonChat Debug] {message}");
    }

    public void OnDisconnected()
    {
        Debug.LogError($"[PhotonChat] 챗 서버 연결 끊김! (원인: {chatClient?.DisconnectedCause})");
    }

    public void OnChatStateChange(ChatState state)
    {
        Debug.Log($"[PhotonChat] 상태 변경됨: {state}");
    }

    public void OnSubscribed(string[] channels, bool[] results)
    {
        // 이 로그가 떴을 때 방장과 참가자의 채널명(channels[0])이 완벽하게 똑같아야 서로 채팅이 보입니다!
        Debug.Log($"<color=cyan>[PhotonChat] 채널 구독 완료! 현재 접속 채널명: {channels[0]}</color>");
    }
    public void OnUnsubscribed(string[] channels) { }
    public void OnStatusUpdate(string user, int status, bool gotMessage, object message) { }
    public void OnUserSubscribed(string channel, string user) { }
    public void OnUserUnsubscribed(string channel, string user) { }
    #endregion
}