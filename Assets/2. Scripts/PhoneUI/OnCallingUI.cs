using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Voice.Unity;

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

        if (Mouse.current.rightButton.wasPressedThisFrame)
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

        gameObject.SetActive(true);
        if (callingListUI != null) callingListUI.SetActive(false);
    }

    private void HandleCallAccepted(string acceptorName)
    {
        if (currentTargetName == acceptorName)
        {
            Debug.Log("[Phone] 상대방이 전화를 받았습니다!");
            isTimerRunning = true;
            timerText.text = "00:00";

            if (VoiceRoomManager.Instance != null)
                VoiceRoomManager.Instance.JoinCallRoom(chatManager.userName, currentTargetName);
        }
    }

    private void HandleHangUp(string callerName)
    {
        timerText.text = "Call Ended";
        isTimerRunning = false;
        isIncomingCall = false;

        if (!gameObject.activeInHierarchy)
        {
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
            return;
        }

        Accept.SetActive(false);
        Reject.SetActive(true);

        if (VoiceRoomManager.Instance != null) VoiceRoomManager.Instance.LeaveCallRoom();

        StartCoroutine(CloseAfterDelay(1.5f));
    }

    private void HandleBusy(string targetName)
    {
        timerText.text = "User Busy";
        isTimerRunning = false;
        isIncomingCall = false;

        if (!gameObject.activeInHierarchy)
        {
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
            return;
        }

        Accept.SetActive(false);
        Reject.SetActive(true);

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    #region 내 조작 로직
    void AcceptCall()
    {
        isTimerRunning = true;
        isIncomingCall = false;

        if (chatManager != null && !string.IsNullOrEmpty(currentTargetName))
        {
            chatManager.SendCallAccept(currentTargetName);
        }

        if (VoiceRoomManager.Instance != null)
            VoiceRoomManager.Instance.JoinCallRoom(chatManager.userName, currentTargetName);
    }

    void RejectOrHangUpCall()
    {
        Accept.SetActive(false);
        Reject.SetActive(true);
        isTimerRunning = false;
        isIncomingCall = false;
        timerText.text = "Call Ended";

        if (chatManager != null && !string.IsNullOrEmpty(currentTargetName))
        {
            chatManager.SendCallHangUp(currentTargetName);
        }

        if (VoiceRoomManager.Instance != null) VoiceRoomManager.Instance.LeaveCallRoom();

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

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