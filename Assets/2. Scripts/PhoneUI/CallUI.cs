using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CallUI : ScrollSelectionUI
{
    [Header("References")]
    public PhotonChatManager chatManager;
    public GameObject highlight;
    public GameObject onCall;

    [Header("Phone Book (임시 목록)")]
    public List<string> phoneBookList = new List<string>();

    private readonly float padding = 100f;
    private Vector3 startPosition;

    private void Awake()
    {
        if (highlight != null) startPosition = highlight.transform.localPosition;

        if (phoneBookList.Count == 0)
        {
            phoneBookList.Add("Player1");
            phoneBookList.Add("Player2");
            phoneBookList.Add("Player3");
            phoneBookList.Add("Player4");
        }
    }

    private void OnEnable()
    {
        if (onCall.activeSelf) onCall.SetActive(false);

        maxIndex = Mathf.Max(0, phoneBookList.Count - 1);
        currentIndex = 0;
        UpdateHighlightVisuals();

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
        if (Mouse.current == null || highlight == null) return;

        HandleScroll();

        if (Mouse.current.rightButton.wasPressedThisFrame) StartCall();
    }

    private void HandleBack()
    {
        PhoneUIController.Instance.ShowScreen(0);
    }

    protected override void UpdateHighlightVisuals()
    {
        Vector3 newPos = startPosition;
        newPos.y -= currentIndex * padding;
        highlight.transform.localPosition = newPos;
    }

    void StartCall()
    {
        if (phoneBookList.Count == 0) return;

        string targetPlayer = phoneBookList[currentIndex];

        if (chatManager != null)
        {
            // [해결] 채팅 서버(신호망)가 아직 준비되지 않았다면 클릭 자체를 무시합니다.
            if (!chatManager.CanChat)
            {
                Debug.LogWarning("[Phone] 통신망 연결 중입니다. 1~2초 뒤에 다시 시도해주세요.");
                return; // 여기서 멈춤 (에러 방지)
            }

            if (targetPlayer == chatManager.userName)
            {
                Debug.LogWarning("[Phone] 자기 자신에게는 전화를 걸 수 없습니다.");
                return;
            }

            chatManager.SendCallRequest(targetPlayer);
        }

        OnCallingUI callingScript = onCall.GetComponent<OnCallingUI>();
        if (callingScript != null)
        {
            callingScript.StartOutgoingCall(targetPlayer);
        }
        else
        {
            onCall.SetActive(true);
        }

        gameObject.SetActive(false);
    }
}