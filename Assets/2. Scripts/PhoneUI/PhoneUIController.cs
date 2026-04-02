using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

enum PhoneScreen
{
    Main,
    Call,
    Message,
    Camera,
    Gallery,
}

public class PhoneUIController : MonoBehaviour
{
    public GameObject phoneUIParent;

    [Header("모든 화면 오브젝트 (Main 포함)")]
    public List<GameObject> allScreens;

    private bool isChatOpen = false;

    private void Start()
    {
        if (phoneUIParent != null) phoneUIParent.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!isChatOpen && Keyboard.current.qKey.wasPressedThisFrame) TogglePhone();
    }

    void TogglePhone()
    {
        if (phoneUIParent == null) return;

        bool isActive = phoneUIParent.activeSelf;
        if (!isActive)
        {
            // 폰을 켤 때만 기본 화면(Main)으로 설정
            ShowScreen(0);
        }
        phoneUIParent.SetActive(!isActive);
    }

    // 화면 전환 함수
    public void ShowScreen(int index)
    {
        foreach (var screen in allScreens)
        {
            // 리스트에 있는 모든 화면을 끄고, 타겟만 켠다 
            screen.SetActive(false);
        }
        allScreens[index].SetActive(true);
    }

    public void Turnoff()
    {
        TogglePhone();
    }

    public void SetChatOpen()
    {
        isChatOpen = !isChatOpen;
    }
}