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

        if (PhoneUIController.Instance.callNotificationObj != null)
        {
            PhoneUIController.Instance.callNotificationObj.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;

        // 화면이 예기치 않게 꺼질 경우 통화 대상의 스피커를 다시 3D로 복구
        if (GlobalVoiceManager.Instance != null && !string.IsNullOrEmpty(currentTargetName))
        {
            GlobalVoiceManager.Instance.SetCallMode(currentTargetName, false);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isIncomingCall && !isTimerRunning) AcceptCall();
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
        targetName.text = target.Split('#')[0];
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
        targetName.text = callerName.Split('#')[0];
        timerText.text = $"Incoming...";

        SoundManager.Instance.PlayLoopSfx(SfxSound.PHONE_CALLALARM);

        if (PhoneUIController.Instance != null)
        {
            if (!PhoneUIController.Instance.phoneUIParent.activeSelf)
            {
                if (PhoneUIController.Instance.callNotificationObj != null)
                    PhoneUIController.Instance.callNotificationObj.SetActive(true);
            }
            else
            {
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

            // [핵심 적용] 전화를 건 입장에서 상대방이 수락하면, 상대방 스피커를 2D로 전환
            if (GlobalVoiceManager.Instance != null)
                GlobalVoiceManager.Instance.SetCallMode(currentTargetName, true);
        }
    }

    private void HandleHangUp(string callerName)
    {
        timerText.text = "Call Ended";
        isTimerRunning = false;
        isIncomingCall = false;

        SoundManager.Instance.StopLoopSfx();
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_REJECT);

        // [핵심 복구] 통화 종료 시 다시 3D로 원상복구
        if (GlobalVoiceManager.Instance != null)
            GlobalVoiceManager.Instance.SetCallMode(currentTargetName, false);

        if (!gameObject.activeInHierarchy)
        {
            if (PhoneUIController.Instance != null) PhoneUIController.Instance.isCallActive = false;
            if (PhoneUIController.Instance.callNotificationObj != null)
                PhoneUIController.Instance.callNotificationObj.SetActive(false);
            return;
        }

        Accept.SetActive(false);
        Reject.SetActive(true);

        StartCoroutine(CloseAfterDelay(1.5f));
    }

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
                PhoneUIController.Instance.isCallActive = false;
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

        // [핵심 적용] 전화를 받는 입장에서 내가 수락하면, 상대방 스피커를 2D로 전환
        if (GlobalVoiceManager.Instance != null)
            GlobalVoiceManager.Instance.SetCallMode(currentTargetName, true);
    }

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

        // [핵심 복구] 내가 끊을 때 다시 3D로 원상복구
        if (GlobalVoiceManager.Instance != null)
            GlobalVoiceManager.Instance.SetCallMode(currentTargetName, false);

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        SoundManager.Instance.StopLoopSfx();

        currentTargetName = "";
        gameObject.SetActive(false);

        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.isCallActive = false;
            PhoneUIController.Instance.phoneUIParent.SetActive(false);
        }

        if (callingListUI != null) callingListUI.SetActive(true);
    }
}