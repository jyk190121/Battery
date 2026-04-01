using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

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
        if (chatInputField != null)
        {
            chatInputField.characterLimit = maxCharacterLimit;
        }
    }

    private void OnEnable()
    {
        // 방에 들어오면 입력창을 비우고 스크롤을 맨 아래로
        chatInputField.text = "";
        StartCoroutine(ScrollToBottom());
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // 엔터 또는 우클릭으로 메시지 전송
        bool isEnterPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        bool isRightClicked = Mouse.current.rightButton.wasPressedThisFrame;

        if (isEnterPressed || isRightClicked)
        {
            SendMessage();
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
        if (string.IsNullOrEmpty(message)) return;

        // 포톤 매니저를 통해 서버로 메시지 발송
        if (chatManager != null)
        {
            chatManager.SendChatMessage(message);
        }

        // 연속으로 채팅을 칠 수 있도록 텍스트 지우고 포커스 유지
        chatInputField.text = "";
        chatInputField.ActivateInputField();
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

        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        // 유니티 UI가 말풍선 크기를 계산할 수 있도록 1프레임 대기
        yield return new WaitForEndOfFrame();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f; // 0이 맨 아래
        }
    }
}