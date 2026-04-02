using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class OnCallingUI : MonoBehaviour
{
    [Header("References")]
    public PhotonChatManager chatManager; // [추가] 포톤 매니저 연결

    public GameObject callingListUI; // CallUI (전화번호부)
    public GameObject Accept;
    public GameObject Reject;
    public TextMeshProUGUI timerText;

    bool isTimerRunning = false;
    float timer = 0f;
    int minutes = 0;

    // 현재 통화 중이거나 전화를 건 상대방의 닉네임
    private string currentTargetName = "";

    private void Awake()
    {
        // 포톤 매니저의 전화 이벤트 구독 (앱이 꺼져 있어도 신호를 받아야 하므로 Awake에 배치)
        PhotonChatManager.OnIncomingCallReceived += HandleIncomingCall;
        PhotonChatManager.OnCallHungUp += HandleHangUp;
    }

    private void OnDestroy()
    {
        // 파괴될 때 이벤트 구독 해제
        PhotonChatManager.OnIncomingCallReceived -= HandleIncomingCall;
        PhotonChatManager.OnCallHungUp -= HandleHangUp;
    }

    private void OnEnable()
    {
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
    }

    private void OnDisable()
    {
        if (callingListUI != null)
        {
            callingListUI.SetActive(true);
        }
        ResetUI();

        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // 수락
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            if (!isTimerRunning) AcceptCall();
        }

        // 거절 또는 끊기
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            RejectOrHangUpCall();
        }

        // 통화 시간 타이머
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

    // UI 상태 초기화
    void ResetUI()
    {
        timer = 0f;
        minutes = 0;
        isTimerRunning = false;
        timerText.text = "Calling...";
        Accept.SetActive(true);
        Reject.SetActive(false);
    }

    #region 네트워크 신호 수신 로직 (상대방이 걸거나 끊었을 때)
    private void HandleIncomingCall(string callerName)
    {
        // 상대방이 나에게 전화를 걸었다!
        currentTargetName = callerName;

        // 폰 전원 켜기 & 통화 화면 띄우기 (만약 폰이 꺼져있다면 켜줌)
        if (PhoneUIController.Instance != null && !PhoneUIController.Instance.phoneUIParent.activeSelf)
        {
            PhoneUIController.Instance.Turnoff(); // 토글로 켜기
        }

        // 내 화면을 OnCallingUI로 강제 이동 (인덱스 1이 Call 스크린이라고 가정, 구조에 맞게 수정 필요)
        gameObject.SetActive(true);
        if (callingListUI != null) callingListUI.SetActive(false);

        timerText.text = $"Incoming...\n{callerName}";
    }

    private void HandleHangUp(string callerName)
    {
        // 상대방이 먼저 전화를 끊었다!
        Debug.Log($"[Phone] {callerName}님이 전화를 끊었습니다.");

        timerText.text = "Call Ended";
        isTimerRunning = false;
        Accept.SetActive(false);
        Reject.SetActive(true);

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    #region 내 조작 로직 (내가 받거나 끊었을 때)
    void AcceptCall()
    {
        Debug.Log("통화 연결됨!");
        isTimerRunning = true;
        Accept.SetActive(false);

        // TODO: 나중에 여기에 Photon Voice 2 오디오 연결 코드가 들어갑니다.
    }

    void RejectOrHangUpCall()
    {
        Debug.Log("통화 거절 / 종료");
        Accept.SetActive(false);
        Reject.SetActive(true);
        isTimerRunning = false;
        timerText.text = "Call Ended";

        // 내가 끊었으므로 서버를 통해 상대방에게도 뚜-뚜- 신호 보내기
        // (누구한테 걸었는지 모르겠다면 PhoneUIController에 저장해둔 이름 사용)
        string target = currentTargetName != "" ? currentTargetName : PhoneUIController.Instance.currentCallerName;

        if (chatManager != null && !string.IsNullOrEmpty(target))
        {
            chatManager.SendCallHangUp(target);
        }

        StartCoroutine(CloseAfterDelay(1.5f));
    }
    #endregion

    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        currentTargetName = ""; // 대상 초기화
        gameObject.SetActive(false);

        // 폰 화면 아예 끄기
        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.phoneUIParent.SetActive(false);
        }
    }
}