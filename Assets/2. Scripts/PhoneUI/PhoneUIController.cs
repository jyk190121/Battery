using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class PhoneUIController : MonoBehaviour
{
    public static PhoneUIController Instance;

    public GameObject phoneUIParent;

    [Header("모든 화면 오브젝트 (Main 포함)")]
    public List<GameObject> allScreens;

    public bool isInputBlocked = false;

    // 통화 중이거나 전화가 오는 중인지 통합 관리
    public bool isCallActive = false;
    public bool isCallRefusing = false;

    public event Action OnBackButtonPressed;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (phoneUIParent != null) phoneUIParent.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (isInputBlocked) return;

        if (Keyboard.current.qKey.wasPressedThisFrame) TogglePhone();

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            OnBackButtonPressed?.Invoke();
        }
    }

    void TogglePhone()
    {
        if (phoneUIParent == null) return;

        bool isActive = phoneUIParent.activeSelf;

        if (isActive && isCallActive && isCallRefusing) return;

        if (!isActive)
        {
            // 전화 관련 이벤트가 활성화되어 있다면 무조건 통화 화면(1번) 오픈
            if (isCallActive) ShowScreen(1);
            else ShowScreen(0);
        }
        phoneUIParent.SetActive(!isActive);
    }

    public void ShowScreen(int index)
    {
        foreach (var screen in allScreens) screen.SetActive(false);
        allScreens[index].SetActive(true);
    }

    public void Turnoff()
    {
        TogglePhone();
    }
}