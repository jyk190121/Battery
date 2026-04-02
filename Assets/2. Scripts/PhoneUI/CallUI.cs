using UnityEngine;
using UnityEngine.InputSystem;

public class CallUI : ScrollSelectionUI
{
    public GameObject highlight;
    public GameObject onCall;

    private readonly float padding = 100f;
    private Vector3 startPosition;

    private void Awake()
    {
        if (highlight != null) startPosition = highlight.transform.localPosition;
        maxIndex = 2; // 부모 클래스 변수 설정
    }

    private void OnEnable()
    {
        if (onCall.activeSelf) onCall.SetActive(false);

        currentIndex = 0;
        UpdateHighlightVisuals();

        // 이벤트 구독
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
    }

    private void OnDisable()
    {
        // 이벤트 해제
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;
    }

    private void Update()
    {
        if (Mouse.current == null || highlight == null) return;

        // 부모 클래스 스크롤 기능
        HandleScroll();

        if (Mouse.current.rightButton.wasPressedThisFrame) StartCall();
    }

    private void HandleBack()
    {
        PhoneUIController.Instance.ShowScreen(0);
    }

    // CallUI는 세로(Y축)로 이동하므로 y값을 뺍니다.
    protected override void UpdateHighlightVisuals()
    {
        Vector3 newPos = startPosition;
        newPos.y -= currentIndex * padding;
        highlight.transform.localPosition = newPos;
    }

    void StartCall()
    {
        onCall.SetActive(true);
        currentIndex = 0;
        UpdateHighlightVisuals();
        gameObject.SetActive(false);
    }
}