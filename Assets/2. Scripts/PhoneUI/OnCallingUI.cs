using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Voice.Unity; // 보이스 라이브러리

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

    // [오디오 처리를 위한 마이크 변수]
    private Recorder myRecorder;

    private void Start()
    {
        // 씬이 시작될 때 맵에 있는 내 마이크(Recorder)를 찾아서 기억해 둡니다.
        myRecorder = FindAnyObjectByType<Recorder>();
    }

    private void Awake()
    {
        PhotonChatManager.OnIncomingCallReceived += HandleIncomingCall;
        PhotonChatManager.OnCallAccepted += HandleCallAccepted;
        PhotonChatManager.OnCallHungUp += HandleHangUp;
    }

    private void OnDestroy()
    {
        PhotonChatManager.OnIncomingCallReceived -= HandleIncomingCall;
        PhotonChatManager.OnCallAccepted -= HandleCallAccepted;
        PhotonChatManager.OnCallHungUp -= HandleHangUp;
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
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (isIncomingCall && !isTimerRunning)
            {
                AcceptCall();
            }
        }

        if (isTimerRunning)
        {
            // V키를 누르는 순간 마이크 ON
            if (Keyboard.current.vKey.wasPressedThisFrame)
            {
                StartAudioConnection();
            }
            // V키에서 손을 떼는 순간 마이크 OFF
            else if (Keyboard.current.vKey.wasReleasedThisFrame)
            {
                StopAudioConnection();
            }

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
        }
    }

    private void HandleHangUp(string callerName)
    {
        timerText.text = "Call Ended";
        isTimerRunning = false;
        isIncomingCall = false;

        Accept.SetActive(false);
        Reject.SetActive(true);

        // 전화가 끊겼으므로 오디오 연결 해제
        StopAudioConnection();

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

        // 전화가 끊겼으므로 오디오 연결 해제
        StopAudioConnection();

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    #region [핵심] 오디오 켜기/끄기 (마이크 전원 관리)
    private void StartAudioConnection()
    {
        // 내 마이크 켜기 (내 목소리를 서버로 전송하기 시작)
        if (myRecorder != null)
        {
            myRecorder.TransmitEnabled = true;
            Debug.Log("[Voice] 통화가 연결되어 마이크를 켭니다.");
        }
    }

    private void StopAudioConnection()
    {
        // 내 마이크 끄기 (전송 중단)
        if (myRecorder != null)
        {
            myRecorder.TransmitEnabled = false;
            Debug.Log("[Voice] 통화가 종료되어 마이크를 끕니다.");
        }
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