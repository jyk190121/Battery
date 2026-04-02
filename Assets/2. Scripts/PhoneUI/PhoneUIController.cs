using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class PhoneUIController : MonoBehaviour
{
    public static PhoneUIController Instance; // 어디서든 즉시 접근 가능한 싱글톤

    public GameObject phoneUIParent;

    [Header("모든 화면 오브젝트 (Main 포함)")]
    public List<GameObject> allScreens;

    // 채팅창 등에서 타이핑 중일 때 단축키(C, Q)를 차단하기 위한 변수
    public bool isInputBlocked = false;

    // [추가된 부분: 전화 알림용 변수] 유저님이 외부에서 자유롭게 가져다 쓰실 수 있는 알림 장치입니다.
    public bool isReceivingCall = false;
    public string currentCallerName = "";

    // C키가 눌렸을 때 다른 앱들에게 뒤로 가기를 실행하라고 알리는 방송(이벤트)
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

        // 채팅 입력(타이핑) 중이라면 아래의 모든 단축키 조작을 완전히 무시함
        if (isInputBlocked) return;

        // Q키: 언제든 폰 켜기/끄기
        if (Keyboard.current.qKey.wasPressedThisFrame) TogglePhone();

        // C키: 현재 켜져 있는 앱에게 뒤로 가기 명령 방송하기
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            OnBackButtonPressed?.Invoke();
        }
    }

    void TogglePhone()
    {
        if (phoneUIParent == null) return;

        bool isActive = phoneUIParent.activeSelf;
        if (!isActive)
        {
            ShowScreen(0); // 폰을 켤 때는 항상 메인 화면으로
        }
        phoneUIParent.SetActive(!isActive);
    }

    public void ShowScreen(int index)
    {
        foreach (var screen in allScreens)
        {
            screen.SetActive(false);
        }
        allScreens[index].SetActive(true);
    }

    public void Turnoff()
    {
        TogglePhone();
    }
}