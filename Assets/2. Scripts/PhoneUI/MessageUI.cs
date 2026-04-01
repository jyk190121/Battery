using NUnit.Framework;
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

    bool isChatOpen = false;

    private void Awake()
    {
        phoneUIController = FindAnyObjectByType<PhoneUIController>();
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        moveScroll();

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (!isChatOpen)
            {
                phoneUIController.ShowScreen(0);
            }
            else
            {
                isChatOpen = false;
                roomList.SetActive(true);
                foreach (var r in room)
                {
                    r.SetActive(false);
                }
            }
        }

        if(Mouse.current.rightButton.wasPressedThisFrame)
        {
            if(!isChatOpen) OpenChat();
        }
    }

    private void OnEnable()
    {
        Reset();
    }

    void moveScroll()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (!isChatOpen)
        {
            if (scrollY != 0)
            {
                if (scrollY > 0)
                {
                    MoveHighlight(-1);
                }
                else if (scrollY < 0)
                {
                    MoveHighlight(1);
                }
            }
        }
    }

    void MoveHighlight(int direction)
    {
        int newIndex = Mathf.Clamp(currentIndex + direction, 0, maxIndex);

        if(newIndex != currentIndex)
        {
            highlightRoom[currentIndex].transform.localScale = Vector3.one; // 이전 하이라이트 원래 크기로
            currentIndex = newIndex;
            highlightRoom[currentIndex].transform.localScale = Vector3.one * 1.1f;
        }
    }

    private void Reset()
    {
        roomList.SetActive(true);
        currentIndex = 0;
        foreach (var highlight in highlightRoom)
        {
            highlight.transform.localScale = Vector3.one; // 모든 하이라이트 원래 크기로
        }
        highlightRoom[currentIndex].transform.localScale = Vector3.one * 1.1f; // 첫 번째 하이라이트 크기 키우기
        foreach(var r in room)
        {
            r.SetActive(false);
        }
    }

    void OpenChat()
    {
        isChatOpen = true;
        roomList.SetActive(false);
        room[currentIndex].SetActive(true);
    }
}



