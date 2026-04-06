using System.Collections.Generic;
using Unity.VectorGraphics.Editor;
using UnityEngine;
using UnityEngine.InputSystem;

public class MessageUI : ScrollSelectionUI
{
    public GameObject roomList;
    public List<GameObject> highlightRoom;
    public List<GameObject> room;

    public bool isChatOpen = false;

    private void Awake()
    {
        maxIndex = 2; // 부모 클래스 변수 설정
    }

    private void OnEnable()
    {
        ResetUI();
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
    }

    private void OnDisable()
    {
        // 화면이 꺼지면 C키 이벤트 구독 해제 (꺼져있을 땐 반응 안함)
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;
    }

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        // 채팅방이 안 열려있을 때만 방 목록 스크롤 이동
        if (!isChatOpen) HandleScroll();

        if (Mouse.current.rightButton.wasPressedThisFrame && !isChatOpen)
        {
            OpenChat();
        }
    }

    // 방송을 수신했을 때 실행될 뒤로 가기 동작
    private void HandleBack()
    {
        if (!isChatOpen) PhoneUIController.Instance.ShowScreen(0);
        else CloseChat();
    }

    // MessageUI는 크기(Scale)를 조절하는 방식으로 시각적 업데이트를 합니다.
    protected override void UpdateHighlightVisuals()
    {
        for (int i = 0; i < highlightRoom.Count; i++)
        {
            // 현재 인덱스만 1.1배로 키우고 나머지는 원래 크기로 돌립니다.
            highlightRoom[i].transform.localScale = (i == currentIndex) ? Vector3.one * 1.1f : Vector3.one;
        }
    }

    private void ResetUI()
    {
        isChatOpen = false;
        roomList.SetActive(true);
        currentIndex = 0;

        UpdateHighlightVisuals();

        foreach (var r in room)
        {
            r.SetActive(false);
        }
    }

    private void OpenChat()
    {
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_SELECT);

        isChatOpen = true;
        roomList.SetActive(false);
        room[currentIndex].SetActive(true);
    }

    public void CloseChat()
    {
        isChatOpen = false;
        roomList.SetActive(true);
        room[currentIndex].SetActive(false);
    }
}