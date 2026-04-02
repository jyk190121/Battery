using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // List 사용

public class CallUI : ScrollSelectionUI
{
    [Header("References")]
    public PhotonChatManager chatManager; //  포톤 매니저 연결
    public GameObject highlight;
    public GameObject onCall;

    [Header("Phone Book (임시 목록)")]
    // 나중에 PhotonNetwork.PlayerList 등을 받아와서 동적으로 채워넣을 전화번호부입니다.
    public List<string> phoneBookList = new List<string>();

    private readonly float padding = 100f;
    private Vector3 startPosition;

    private void Awake()
    {
        if (highlight != null) startPosition = highlight.transform.localPosition;

        // 임시 테스트용 데이터 (실제 게임에선 방 접속 시 동기화)
        if (phoneBookList.Count == 0)
        {
            phoneBookList.Add("Player2");
            phoneBookList.Add("Player3");
            phoneBookList.Add("Player4");
        }
    }

    private void OnEnable()
    {
        if (onCall.activeSelf) onCall.SetActive(false);

        // 전화번호부 인원수만큼 스크롤 최대 인덱스 설정
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
        // 1. 방에 아무도 없거나 리스트가 비었으면 무시
        if (phoneBookList.Count == 0) return;

        // 2. 현재 하이라이트된 닉네임 가져오기
        string targetPlayer = phoneBookList[currentIndex];

        // 3. 포톤 서버를 통해 상대방에게 전화 연결 신호 쏘기!
        if (chatManager != null)
        {
            chatManager.SendCallRequest(targetPlayer);
        }

        // 4. 내 폰 화면을 '전화 거는 중' UI로 변경
        onCall.SetActive(true);
        gameObject.SetActive(false); // CallUI 목록창은 끕니다.
    }
}