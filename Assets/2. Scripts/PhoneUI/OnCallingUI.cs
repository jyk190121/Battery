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

    // 내가 전화를 '받아야 하는' 상황인지 기억하는 상태 변수
    private bool isIncomingCall = false;

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

        // 내가 '수신자(isIncomingCall)'이고 아직 '타이머가 안 돌아갈 때'만 우클릭으로 받을 수 있음
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (isIncomingCall && !isTimerRunning)
            {
                AcceptCall();
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
        isIncomingCall = false; // 내가 거는 쪽이므로 받을 수 없음
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
        isIncomingCall = true; // 전화를 받는 쪽이므로 우클릭 수락 대기 상태로 전환

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
        isIncomingCall = false; // 통화가 끝났으므로 수신 대기 해제

        Accept.SetActive(false);
        Reject.SetActive(true);

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    #region 내 조작 로직
    void AcceptCall()
    {
        isTimerRunning = true;
        isIncomingCall = false; // 전화를 받았으므로 더 이상 수신 대기 상태가 아님

        if (chatManager != null && !string.IsNullOrEmpty(currentTargetName))
        {
            chatManager.SendCallAccept(currentTargetName);
        }

        // TODO: Photon Voice 2 오디오 연결 코드
    }

    void RejectOrHangUpCall()
    {
        PhoneUIController.Instance.isCallRefusing = true; // 전화 거절/끊기 상태로 전환
        Accept.SetActive(false);
        Reject.SetActive(true);
        isTimerRunning = false;
        isIncomingCall = false; //  상태 초기화
        timerText.text = "Call Ended";

        if (chatManager != null && !string.IsNullOrEmpty(currentTargetName))
        {
            chatManager.SendCallHangUp(currentTargetName);
        }

        if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        currentTargetName = "";
        isIncomingCall = false; // 안전장치 초기화
        gameObject.SetActive(false);

        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.phoneUIParent.SetActive(false);
            PhoneUIController.Instance.isCallActive = false;
            PhoneUIController.Instance.isCallRefusing = false; // 전화 거절/끊기 상태 초기화
        }

        if (callingListUI != null) callingListUI.SetActive(true);

    }
}