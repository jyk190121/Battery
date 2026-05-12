using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TeamChatRoomUI : MonoBehaviour
{
    [Header("Network")]
    public PhotonChatManager chatManager; // 포톤 매니저 연결

    [Header("UI Components")]
    public TMP_InputField chatInputField;
    public ScrollRect scrollRect;
    public Transform contentTransform;

    [Header("Prefabs")]
    public GameObject myBubblePrefab;    // 내 말풍선 (우측 정렬)
    public GameObject otherBubblePrefab; // 상대방 말풍선 (좌측 정렬)

    [Header("Settings")]
    public int maxCharacterLimit = 100;
    public float scrollSpeed = 0.1f;

    // 입력 상태에서 채팅방을 나갔다가 다시 들어왔을 때, 이전에 입력하던 텍스트가 남아있는 문제 방지용 해시값
    private int lastOpenedTimeHash = -1;

    private void Awake()
    {
        if (chatInputField != null) chatInputField.characterLimit = maxCharacterLimit;

        chatInputField.onValueChanged.AddListener(OnTyping);
    }


    private void OnEnable()
    {
        // 폰 화면이 켜질 때마다 날짜를 체크하여 청소합니다.
        if (GameMaster.Instance != null && GameMaster.Instance.dayCycleManager != null)
        {
            int currentDay = GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
            int currentWeek = GameMaster.Instance.completedCycleCount.Value;

            // 주차와 일차를 합쳐 고유한 시간값을 만듭니다 (예: 1주차 3일 = 103)
            int currentTimeHash = (currentWeek * 100) + currentDay;

            // 마지막으로 열었던 시간과 다르면 (즉, 날이 바뀌었으면)
            if (lastOpenedTimeHash != -1 && lastOpenedTimeHash != currentTimeHash)
            {
                ClearChatHistory(); // 폰 켜자마자 즉시 화면 청소
            }

            lastOpenedTimeHash = currentTimeHash; // 방금 연 시간으로 갱신
        }

        // UI 초기화 로직
        chatInputField.text = "";
        chatInputField.gameObject.SetActive(false);
        StartCoroutine(ScrollToBottom());

        if (PhoneUIController.Instance != null && PhoneUIController.Instance.messageNotificationObj != null)
        {
            PhoneUIController.Instance.messageNotificationObj.SetActive(false);

            if (PhoneUIController.Instance.messageNotificationMobile != null)
                PhoneUIController.Instance.messageNotificationMobile.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // 채팅방 화면이 꺼질 때 꼬임 방지를 위해 입력 차단을 무조건 강제로 풀어줍니다.
        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.isInputBlocked = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        bool isEnterPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        bool isLeftClicked = Mouse.current.leftButton.wasPressedThisFrame;

        if (isEnterPressed || isLeftClicked)
        {
            if (!chatInputField.gameObject.activeSelf)
            {
                SoundManager.Instance.PlaySfx(SfxSound.PHONE_TYPING_START);
                // [입력 상태 진입] 비활성화 상태였다면 켜고 포커스 주기
                chatInputField.gameObject.SetActive(true);
                chatInputField.ActivateInputField();

                // 타이핑 시작: 사령탑에 단축키(C, Q)를 차단하라고 지시
                if (PhoneUIController.Instance != null) PhoneUIController.Instance.isInputBlocked = true;
            }
            else
            {
                // [입력 종료 및 전송] 활성화 상태였다면 메시지를 보내고 다시 끄기
                SendMessage();
            }
        }

        HandleScrolling();
    }

    private void HandleScrolling()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (scrollY != 0 && scrollRect != null)
        {
            float newPos = scrollRect.verticalNormalizedPosition + (scrollY > 0 ? scrollSpeed : -scrollSpeed);
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
        }
    }

    private void SendMessage()
    {
        string message = chatInputField.text.Trim();

        // 텍스트가 비어있지 않을 때만 서버로 발송
        if (!string.IsNullOrEmpty(message))
        {
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_MESSAGE_SEND);
            if (chatManager != null)
            {
                chatManager.SendChatMessage(message);
            }
        }

        // 전송 여부(빈칸 여부)와 상관없이 텍스트를 초기화하고 입력창 비활성화
        chatInputField.text = "";
        chatInputField.gameObject.SetActive(false);

        // 메시지 전송 후 다시 단축키 사용 허용
        if (PhoneUIController.Instance != null) PhoneUIController.Instance.isInputBlocked = false;
    }

    // 포톤 매니저가 서버로부터 메시지를 받았을 때 호출하는 함수
    public void ReceiveMessage(string senderName, string messageText, bool isMine)
    {
        // '#'을 기준으로 문자열을 잘라 앞부분(순수 닉네임)만 가져옵니다.
        string displayNickname = senderName.Split('#')[0];

        // senderName 대신 displayNickname을 사용
        string formattedMessage = isMine ? messageText : $"<b>{displayNickname}</b>\n{messageText}";

        GameObject prefabToUse = isMine ? myBubblePrefab : otherBubblePrefab;
        CreateSpeechBubble(prefabToUse, formattedMessage);
    }

    private void CreateSpeechBubble(GameObject prefab, string text)
    {
        GameObject bubble = Instantiate(prefab, contentTransform);
        TextMeshProUGUI tmp = bubble.GetComponentInChildren<TextMeshProUGUI>();

        if (tmp != null)
        {
            tmp.text = text;
        }

        // 텍스트가 입력된 직후, 자식들의 크기와 위치를 즉시 다시 계산하도록 강제합니다.
        // 이거 안하면 첫번째는 어긋나고, 두번째부터 자리 잡음
        if (gameObject.activeInHierarchy)
        {
            // 텍스트가 입력된 직후, 자식들의 크기와 위치를 즉시 다시 계산하도록 강제합니다.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform.GetComponent<RectTransform>());

            StartCoroutine(ScrollToBottom());
        }
    }

    private IEnumerator ScrollToBottom()
    {
        // 유니티 UI가 말풍선 크기를 계산할 수 있도록 1프레임 대기
        yield return null;

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f; // 0이 맨 아래
        }
    }


    // 입력 필드에 타이핑이 시작될 때마다 호출되는 함수
    private void OnTyping(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_TYPING);
        }
    }

    // 폰이 켜질 때(OnEnable) 호출되어 말풍선을 지웁니다.
    private void ClearChatHistory()
    {
        if (contentTransform != null)
        {
            foreach (Transform child in contentTransform)
            {
                Destroy(child.gameObject);
            }
            Debug.Log("<color=cyan>[TeamChatRoomUI]</color> 날짜가 변경되어 이전 채팅 내역을 초기화했습니다.");
        }
    }
}