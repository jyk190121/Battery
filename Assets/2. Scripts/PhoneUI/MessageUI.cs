using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MessageUI : MonoBehaviour
{
    private PhoneUIController phoneUIController;

    public GameObject roomList;
    public List<GameObject> highlightRoom;
    public List<GameObject> room;

    private int currentIndex = 0;
    private readonly int maxIndex = 2;

    public bool isChatOpen = false;

    private void Awake()
    {
        phoneUIController = FindAnyObjectByType<PhoneUIController>();
    }

    private void OnEnable()
    {
        Reset();
    }

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        // 채팅방이 안 열려있을 때만 방 목록 스크롤 이동
        if (!isChatOpen)
        {
            moveScroll();
        }

        // C키로 뒤로가기 또는 앱 종료
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (!isChatOpen)
            {
                phoneUIController.ShowScreen(0);
            }
            else
            {
                CloseChat();
            }
        }

        // 채팅방이 안 열려있을 때 우클릭으로 방 입장
        if (Mouse.current.rightButton.wasPressedThisFrame && !isChatOpen)
        {
            OpenChat();
        }
    }

    private void moveScroll()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (scrollY > 0) MoveHighlight(-1);
        else if (scrollY < 0) MoveHighlight(1);
    }

    private void MoveHighlight(int direction)
    {
        int newIndex = Mathf.Clamp(currentIndex + direction, 0, maxIndex);

        if (newIndex != currentIndex)
        {
            highlightRoom[currentIndex].transform.localScale = Vector3.one;
            currentIndex = newIndex;
            highlightRoom[currentIndex].transform.localScale = Vector3.one * 1.1f;
        }
    }

    private void Reset()
    {
        isChatOpen = false;
        roomList.SetActive(true);
        currentIndex = 0;

        foreach (var highlight in highlightRoom)
        {
            highlight.transform.localScale = Vector3.one;
        }
        if (highlightRoom.Count > 0)
        {
            highlightRoom[currentIndex].transform.localScale = Vector3.one * 1.1f;
        }

        foreach (var r in room)
        {
            r.SetActive(false);
        }
    }

    private void OpenChat()
    {
        isChatOpen = true;
        roomList.SetActive(false);
        room[currentIndex].SetActive(true);
    }

    private void CloseChat()
    {
        isChatOpen = false;
        roomList.SetActive(true);
        room[currentIndex].SetActive(false);
    }
}