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
        PhotonChatManager.OnCallBusy += HandleBusy; // [추가]
    }

    private void OnDestroy()
    {
        PhotonChatManager.OnIncomingCallReceived -= HandleIncomingCall;
        PhotonChatManager.OnCallAccepted -= HandleCallAccepted;
        PhotonChatManager.OnCallHungUp -= HandleHangUp;
        PhotonChatManager.OnCallBusy -= HandleBusy; // [추가]
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

        // 폰 화면이 꺼질 때 마이크가 켜져있다면 강제 종료 (안전장치)
        if (myRecorder != null) myRecorder.TransmitEnabled = false;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (isIncomingCall && !isTimerRunning) AcceptCall();
        }

        // [핵심 4] 절대 꼬이지 않는 V키 상태 동기화 방식
        if (myRecorder != null)
        {
            // 통화 타이머가 돌아가는 중이면서 && 물리적인 V키가 꾹 눌려있을 때만 true
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

            // [추가] 통화가 시작되면 비밀 1:1 보이스 방으로 둘 다 입장!
            if (VoiceRoomManager.Instance != null)
                VoiceRoomManager.Instance.JoinCallRoom(chatManager.userName, currentTargetName);
        }
    }

    private void HandleHangUp(string callerName)
    {
        timerText.text = "Call Ended";
        isTimerRunning = false;
        isIncomingCall = false;

        Accept.SetActive(false);
        Reject.SetActive(true);

        // [추가] 통화가 끊기면 1:1 방에서 퇴장
        if (VoiceRoomManager.Instance != null) VoiceRoomManager.Instance.LeaveCallRoom();

        StartCoroutine(CloseAfterDelay(1.5f));
    }

    // [핵심 2] 상대방이 바쁠 때 거절 처리
    private void HandleBusy(string targetName)
    {
        timerText.text = "User Busy"; // 화면에 바쁘다고 표시
        isTimerRunning = false;
        isIncomingCall = false;

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

        // 전화를 받은 사람도 1:1 보이스 방으로 입장!
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

        if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;

        // [추가] 내가 전화를 끊었을 때도 1:1 방에서 퇴장
        if (VoiceRoomManager.Instance != null) VoiceRoomManager.Instance.LeaveCallRoom();

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        currentTargetName = "";
        gameObject.SetActive(false);

        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.phoneUIParent.SetActive(false);
            PhoneUIController.Instance.isCallActive = false;
        }

        if (callingListUI != null) callingListUI.SetActive(true);
    }
}