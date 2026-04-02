using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class OnCallingUI : MonoBehaviour
{
    public GameObject callingListUI;

    public GameObject Accept;
    public GameObject Reject;

    public TextMeshProUGUI timerText;

    bool isTimerRunning = false;
    float timer = 0f;
    int minutes = 0;

    private void OnEnable()
    {
        // [이벤트 구독]
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
    }

    private void OnDisable()
    {
        if (callingListUI != null)
        {
            callingListUI.SetActive(true);
        }
        Reset();

        // [이벤트 해제]
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            AcceptCall();
        }

        // F2키로 거절. (C키 기능은 HandleBack 이벤트로 이전됨)
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            RejectCall();
        }

        if (isTimerRunning)
        {
            timer += Time.deltaTime;
            minutes = Mathf.FloorToInt(timer / 60f);
            float seconds = timer % 60f;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    // 방송 수신 시 전화 거절 처리
    private void HandleBack()
    {
        RejectCall();
    }

    // 전화 수신 UI 초기화
    void Reset()
    {
        timer = 0f;
        minutes = 0;
        isTimerRunning = false;
        timerText.text = "Calling...";
        Accept.SetActive(true);
        Reject.SetActive(false);
    }

    void AcceptCall()
    {
        // 수신 수락
        Debug.Log("Accepted");
        isTimerRunning = true;
    }

    void RejectCall()
    {
        // 수신 거절
        Debug.Log("Rejected");
        Accept.SetActive(false);
        Reject.SetActive(true);
        isTimerRunning = false;
        timerText.text = "Call Rejected";

        StartCoroutine(CloseAfterDelay(2f));
    }
    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        gameObject.SetActive(false);

        // 싱글톤으로 직접 호출
        if (PhoneUIController.Instance != null)
        {
            PhoneUIController.Instance.Turnoff();
        }
    }
}
