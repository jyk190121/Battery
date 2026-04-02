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
            chatManager.SendCallRequest(targetPlayer);
        }

        // OnCallingUI에게 내가 누굴 호출했는지 알려주고 발신자 전용 UI로 세팅시킴
        OnCallingUI callingScript = onCall.GetComponent<OnCallingUI>();
        if (callingScript != null)
        {
            callingScript.StartOutgoingCall(targetPlayer);
        }
        else
        {
            onCall.SetActive(true); // 안전장치
        }

        gameObject.SetActive(false);
    }
}