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

    private void Awake()
    {
        if (chatInputField != null) chatInputField.characterLimit = maxCharacterLimit;

        chatInputField.onValueChanged.AddListener(OnTyping);
    }


    private void OnEnable()
    {
        // 방에 들어오면 스크롤을 맨 아래로 맞추고, 입력창은 숨김(비활성화) 상태로 시작
        chatInputField.text = "";
        chatInputField.gameObject.SetActive(false);
        StartCoroutine(ScrollToBottom());

        if (PhoneUIController.Instance != null && PhoneUIController.Instance.messageNotificationObj != null)
        {
            // 채팅방 화면이 켜질 때 알림이 켜져 있다면 끄기
            PhoneUIController.Instance.messageNotificationObj.SetActive(false);

            // 모바일 알림도 끄기 
            if (PhoneUIController.Instance.messageNotificationMobile != null)
            {
                PhoneUIController.Instance.messageNotificationMobile.SetActive(false);
            }
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
        // 4명 중 누가 보냈는지 알 수 있도록 닉네임 결합
        string formattedMessage = isMine ? messageText : $"<b>{senderName}</b>\n{messageText}";

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
}