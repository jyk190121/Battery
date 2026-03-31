using UnityEngine;
using UnityEngine.InputSystem;

public class CallUI : MonoBehaviour
{
    public GameObject highlight;
    public GameObject onCall;

    private int currentIndex = 0;
    private readonly int maxIndex = 2;
    private readonly float padding = 100f;

    private Vector3 startPosition;    // 하이라이트 초기 위치

    private PhoneUIController phoneUIController;

    private void Awake()
    {
        if (highlight != null)
        {
            startPosition = highlight.transform.localPosition; // 초기 위치 저장
        }
        phoneUIController = FindAnyObjectByType<PhoneUIController>();
    }

    private void Update()
    {
        if (Mouse.current == null || highlight == null) return;

        moveScroll();

        if(Mouse.current.rightButton.wasPressedThisFrame)
        {
            StartCall();
        }

    }

    #region 하이라이트 이동 처리
    private void moveScroll()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;

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

    private void MoveHighlight(int direction)
    {
        // 인덱스를 변경하고 0~3 사이로 제한 (1번에서 4번으로 바로 못 가게 함)
        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, maxIndex);

        // 인덱스가 실제로 변했을 때만 위치 변경
        if (nextIndex != currentIndex)
        {
            currentIndex = nextIndex;
            UpdateHighlightPosition();
        }
    }

    private void UpdateHighlightPosition()
    {
        // 시작 위치에서 (인덱스 * 간격)만큼 X축으로 이동
        Vector3 newPos = startPosition;
        newPos.y -= currentIndex * padding;

        highlight.transform.localPosition = newPos;
    }
    #endregion

    void StartCall()
    {
        onCall.SetActive(true);    // 통화 화면으로 전환
        currentIndex = 0;
        UpdateHighlightPosition(); // 하이라이트 위치 초기화
        gameObject.SetActive(false);
    }
}
