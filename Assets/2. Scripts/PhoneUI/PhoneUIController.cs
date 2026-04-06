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

    [Header("전화 화면 (OnCallingUI가 있는 오브젝트)")]
    public GameObject onCallingUIObject;

    [Header("알림 UI")]
    public GameObject callNotificationObj;
    public GameObject messageNotificationObj;
    public GameObject messageNotificationMobile;

    public bool isInputBlocked = false;

    // 통화 중이거나 전화가 오는 중인지 통합 관리
    public bool isCallActive = false;
    public bool isCallRefusing = false;

    public event Action OnBackButtonPressed;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (phoneUIParent != null)
        {
            phoneUIParent.SetActive(true); // 1. 휴대폰 최상위 부모 켜기

            // 2. CallUI 앱 화면 켜기 (인덱스 1번이라 가정)
            if (allScreens.Count > 1 && allScreens[1] != null)
            {
                allScreens[1].SetActive(true);

                // 3. OnCallingUI 켜기 (모든 부모가 켜져있으므로 비로소 Awake가 실행됨)
                if (onCallingUIObject != null)
                {
                    onCallingUIObject.SetActive(true);
                    onCallingUIObject.SetActive(false); // 4. Awake 실행 후 즉시 끄기
                }

                allScreens[1].SetActive(false); // 5. CallUI 다시 끄기
            }
        }
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

        // OnCallingUI가 달린 오브젝트가 실제로 켜져 있으면 끄는 기능 무조건 차단
        if (isActive && onCallingUIObject != null && onCallingUIObject.activeInHierarchy) return;

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