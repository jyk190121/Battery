using Photon.Voice.Unity;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class OnCallingUI : MonoBehaviour
{
    [Header("References")]
    public PhotonChatManager chatManager;
    public GameObject callingListUI;
    public GameObject Accept;
    public GameObject Reject;
    public TextMeshProUGUI targetName;
    public TextMeshProUGUI timerText;

    bool isTimerRunning = false;
    float timer = 0f;
    int minutes = 0;

    private string currentTargetName = "";
    private bool isIncomingCall = false;

    private Recorder myRecorder;

    private void Start()
    {
        myRecorder = FindAnyObjectByType<Recorder>();
    }

    private void Awake()
    {
        PhotonChatManager.OnIncomingCallReceived += HandleIncomingCall;
        PhotonChatManager.OnCallAccepted += HandleCallAccepted;
        PhotonChatManager.OnCallHungUp += HandleHangUp;
        PhotonChatManager.OnCallBusy += HandleBusy;
    }

    private void OnDestroy()
    {
        PhotonChatManager.OnIncomingCallReceived -= HandleIncomingCall;
        PhotonChatManager.OnCallAccepted -= HandleCallAccepted;
        PhotonChatManager.OnCallHungUp -= HandleHangUp;
        PhotonChatManager.OnCallBusy -= HandleBusy;
    }

    private void OnEnable()
    {
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
        // OnCalling 화면을 보면 전화 알림 끄기
        if (PhoneUIController.Instance.callNotificationObj != null)
        {
            PhoneUIController.Instance.callNotificationObj.SetActive(false);
        }

    }

    private void OnDisable()
    {
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;

        if (myRecorder != null) myRecorder.TransmitEnabled = false;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isIncomingCall && !isTimerRunning) AcceptCall();
        }

        if (myRecorder != null)
        {
            bool shouldTransmit = isTimerRunning && Keyboard.current.vKey.isPressed;

            if (myRecorder.TransmitEnabled != shouldTransmit)
            {
                myRecorder.TransmitEnabled = shouldTransmit;
            }
        }

        if (isTimerRunning)
        {
            timer += Time.deltaTime;
            minutes = Mathf.FloorToInt(timer / 60f);
            float seconds = timer % 60f;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void HandleBack()
    {
        RejectOrHangUpCall();
    }

    #region 발신자 / 수신자 모드 세팅
    public void StartOutgoingCall(string target)
    {
        currentTargetName = target;
        targetName.text = target;
        timer = 0f;
        minutes = 0;
        isTimerRunning = false;
        isIncomingCall = false;
        timerText.text = "Calling...";

        SoundManager.Instance.PlayLoopSfx(SfxSound.PHONE_DIAL);

        Accept.SetActive(true);
        Reject.SetActive(false);

        gameObject.SetActive(true);
        if (callingListUI != null) callingListUI.SetActive(false);
    }

    void SetReceiverMode()
    {
        timer = 0f;
        minutes = 0;
        isTimerRunning = false;
        isIncomingCall = true;

        Accept.SetActive(true);
        Reject.SetActive(false);
    }
    #endregion

    #region 네트워크 신호 수신 로직
    private void HandleIncomingCall(string callerName)
    {
        currentTargetName = callerName;
        SetReceiverMode();
        targetName.text = callerName;
        timerText.text = $"Incoming...";

        SoundManager.Instance.PlayLoopSfx(SfxSound.PHONE_CALLALARM);

        // 폰 상태에 따른 알림 및 화면 전환 처리 
        if (PhoneUIController.Instance != null)
        {
            if (!PhoneUIController.Instance.phoneUIParent.activeSelf)
            {
                // 휴대폰이 내려가 있는 경우 알림 표시
                if (PhoneUIController.Instance.callNotificationObj != null)
                    PhoneUIController.Instance.callNotificationObj.SetActive(true);
            }
            else
            {
                // 휴대폰이 올려져 있는 경우 즉시 통화 앱(인덱스 1 가정)으로 화면 강제 전환
                PhoneUIController.Instance.ShowScreen(1);
            }
        }

        gameObject.SetActive(true);
        if (callingListUI != null) callingListUI.SetActive(false);
    }

    private void HandleCallAccepted(string acceptorName)
    {
        if (currentTargetName == acceptorName)
        {
            Debug.Log("[Phone] 상대방이 전화를 받았습니다!");

            SoundManager.Instance.StopLoopSfx();
            isTimerRunning = true;
            timerText.text = "00:00";

            if (VoiceRoomManager.Instance != null)
                VoiceRoomManager.Instance.JoinCallRoom(chatManager.userName, currentTargetName);
        }
    }
    // 상대방이 통화 중이거나, 전화가 울리는 도중에 끊었을 때 처리 로직
    private void HandleHangUp(string callerName)
    {
        timerText.text = "Call Ended";
        isTimerRunning = false;
        isIncomingCall = false;

        SoundManager.Instance.StopLoopSfx();
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_REJECT);

        if (!gameObject.activeInHierarchy)
        {
            // UI가 켜져 있지 않은 상태에서 상대방이 끊은 경우, 통화 상태를 풀어주기
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
            // UI가 켜져 있지 않은 상태에서 상대방이 끊은 경우, 알림이 켜져 있다면 끄기
            if (PhoneUIController.Instance.callNotificationObj != null)
                PhoneUIController.Instance.callNotificationObj.SetActive(false);
            return;
        }

        Accept.SetActive(false);
        Reject.SetActive(true);

        if (VoiceRoomManager.Instance != null) VoiceRoomManager.Instance.LeaveCallRoom();

        StartCoroutine(CloseAfterDelay(1.5f));
    }

    // 상대방이 통화 중일 때 처리 로직
    private void HandleBusy(string targetName)
    {
        timerText.text = "User Busy";
        isTimerRunning = false;
        isIncomingCall = false;

        SoundManager.Instance.StopLoopSfx();
        SoundManager.Instance.PlayLoopSfx(SfxSound.PHONE_BUSY);

        if (!gameObject.activeInHierarchy)
        {
            if (PhoneUIController.Instance != null)
            {
                // UI가 켜져 있지 않은 상태에서 상대방이 통화 중인 경우, 통화 상태를 풀어주기
                PhoneUIController.Instance.isCallActive = false;

                // 폰이 내려가 있는 상태에서 상대방이 통화 중일 때 알림 끄기 방어 로직 추가
                if (PhoneUIController.Instance.callNotificationObj != null)
                    PhoneUIController.Instance.callNotificationObj.SetActive(false);
            }
            return;
        }

        Accept.SetActive(false);
        Reject.SetActive(true);

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    #region 내 조작 로직
    // 수신자 입장에서 전화 받기
    void AcceptCall()
    {
        isTimerRunning = true;
        isIncomingCall = false;

        SoundManager.Instance.StopLoopSfx();
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_ACCEPT);

        if (chatManager != null && !string.IsNullOrEmpty(currentTargetName))
        {
            chatManager.SendCallAccept(currentTargetName);
        }

        if (VoiceRoomManager.Instance != null)
            VoiceRoomManager.Instance.JoinCallRoom(chatManager.userName, currentTargetName);
    }
    // 발신자 입장에서, 또는 수신자 입장에서 통화 거절 또는 통화 중에 끊기
    void RejectOrHangUpCall()
    {
        Accept.SetActive(false);
        Reject.SetActive(true);
        isTimerRunning = false;
        isIncomingCall = false;
        timerText.text = "Call Ended";

        SoundManager.Instance.StopLoopSfx();
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_REJECT);

        if (chatManager != null && !string.IsNullOrEmpty(currentTargetName))
        {
            chatManager.SendCallHangUp(currentTargetName);
        }

        if (VoiceRoomManager.Instance != null) VoiceRoomManager.Instance.LeaveCallRoom();

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion
    // UI가 닫히는 순간에 통화 상태를 풀어주는 코루틴
    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        SoundManager.Instance.StopLoopSfx();

        currentTargetName = "";
        gameObject.SetActive(false);

        // 1.5초가 지나고 UI가 완전히 닫히는 이 순간에만 통화 상태를 풉니다. 
        // 이제 1.5초 대기 중에 아무리 Q키를 눌러도 UI가 강제로 닫히며 꼬이지 않습니다.
        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.isCallActive = false;
            PhoneUIController.Instance.phoneUIParent.SetActive(false);
        }

        if (callingListUI != null) callingListUI.SetActive(true);
    }
}