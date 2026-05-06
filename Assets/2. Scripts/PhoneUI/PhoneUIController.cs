using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public event Action OnFlashlightToggleRequested;   // 우클릭: 라이트 토글

    public bool isPhoneActive = false; 

    [Header("전력 시스템")]
    public bool hasPower = true;

    // 외부 UI(휴대폰 상태에 따라 켜지고 꺼지는 오브젝트) 제어용 이벤트
    public event Action<bool> OnPhoneStateChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this; 

        if (phoneUIParent != null) 
        {
            phoneUIParent.SetActive(true); // 1. 휴대폰 최상위 부모 켜기[cite: 1]

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

        // 배터리 방전 이벤트 구독
        if (PhoneBatteryController.Instance != null)
        {
            PhoneBatteryController.Instance.OnBatteryEmpty += HandleBatteryEmpty;
        }
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지를 위한 이벤트 구독 해제
        if (PhoneBatteryController.Instance != null)
        {
            PhoneBatteryController.Instance.OnBatteryEmpty -= HandleBatteryEmpty;
        }
    }

    private void Update()
    {
        if (GameMaster.Instance == null) return; 
        if (Keyboard.current == null) return; 
        if (isInputBlocked) return; 

        if (Keyboard.current.qKey.wasPressedThisFrame) 
        {
            // 배터리가 없으면 켜지지 않도록 차단
            if (!hasPower)
            {
                Debug.Log("배터리가 없어 폰을 켤 수 없습니다.");
                SoundManager.Instance.PlaySfx(SfxSound.PHONE_ERROR);
                return;
            }

            if (!isPhoneActive && PlayerInventory.IsHoldingTwoHanded) 
            {
                Debug.Log("양손을 사용중이라 스마트폰 사용이 불가합니다."); 
                return; 
            }

            TogglePhone(); 
        }

        // 휴대폰이 활성화된 상태에서만 작동
        if (!phoneUIParent.activeSelf) return;

        // 1. C 키: 뒤로가기 (앱 종료 등)
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_RETURN); 
            OnBackButtonPressed?.Invoke(); 
        }

        // 2. 마우스 우클릭: 라이트 토글 이벤트 알림 전송
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            OnFlashlightToggleRequested?.Invoke(); 
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
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_OPEN); 
            if (isCallActive) ShowScreen(1); 
            else ShowScreen(0); 
        }
        else 
        {
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_CLOSE); 
        }

        phoneUIParent.SetActive(!isActive); 
        isPhoneActive = !isActive; 

        // 폰 상태가 바뀔 때마다 외부 UI에 알림 방송
        OnPhoneStateChanged?.Invoke(isPhoneActive);
    }

    // PhoneBatteryController에서 방전 알림을 받을 때 실행
    private void HandleBatteryEmpty()
    {
        hasPower = false; // 전력 차단

        // 폰이 켜져 있다면 강제 종료
        if (isPhoneActive)
        {
            ForceTurnOff();
        }
    }

    // 기존 방어 로직(통화 중 차단)을 무시하고 무조건 끄는 강제 종료 함수
    private void ForceTurnOff()
    {
        if (phoneUIParent == null) return;

        // 배터리 방전 시 발생할 수 있는 백그라운드 통화 버그 방지를 위해 상태 초기화
        isCallActive = false;
        isCallRefusing = false;

        SoundManager.Instance.PlaySfx(SfxSound.PHONE_CLOSE); // 전원 꺼짐 사운드로 대체 가능

        phoneUIParent.SetActive(false);
        isPhoneActive = false;

        // 외부 UI들에게도 폰이 꺼졌음을 알림
        OnPhoneStateChanged?.Invoke(false);
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