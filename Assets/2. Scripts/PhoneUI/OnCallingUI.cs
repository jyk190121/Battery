using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class OnCallingUI : MonoBehaviour
{
    private PhoneUIController phoneUIController;

    public GameObject callingListUI;

    public GameObject Accept;
    public GameObject Reject;

    public TextMeshProUGUI timerText;

    bool isTimerRunning = false;
    float timer = 0f;
    int minutes = 0;
    private void Awake()
    {
        phoneUIController = FindAnyObjectByType<PhoneUIController>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if(Keyboard.current.f1Key.wasPressedThisFrame)
        {
            // 수신 수락
            Debug.Log("Accepted");
            isTimerRunning = true;
        }

        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            // 수신 거절
            Debug.Log("Rejected");
            Accept.SetActive(false);
            Reject.SetActive(true);
            isTimerRunning = false;
            timerText.text = "Call Rejected";

            StartCoroutine(CloseAfterDelay(2f));
        }

        if(isTimerRunning)
        {
            timer += Time.deltaTime;
            minutes = Mathf.FloorToInt(timer / 60f);
            float seconds = timer % 60f;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private IEnumerator CloseAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        gameObject.SetActive(false); // 이 오브젝트를 끕니다. (OnDisable이 자동으로 호출됨)
        phoneUIController.Turnoff(); // 폰 UI를 끕니다.
    }

    private void OnDisable()
    {
        if (callingListUI != null)
        {
            callingListUI.SetActive(true);
        }
        Reset();
    }

    void Reset()
    {
        timer = 0f;
        minutes = 0;
        isTimerRunning = false;
        timerText.text = "Calling...";
        Accept.SetActive(true);
        Reject.SetActive(false);
    }
}
