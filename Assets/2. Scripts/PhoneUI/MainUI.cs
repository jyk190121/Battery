using UnityEngine;
using UnityEngine.InputSystem;

public class MainUI : ScrollSelectionUI
{
    public GameObject highlight;
    private readonly float padding = 85f;
    private Vector3 startPosition;

    private void Awake()
    {
        if (highlight != null) startPosition = highlight.transform.localPosition;

        // 부모 클래스의 변수 설정
        maxIndex = 3;
    }

    private void OnEnable()
    {
        currentIndex = 0;
        UpdateHighlightVisuals();
    }

    private void Update()
    {
        if (Mouse.current == null || highlight == null) return;

        // 부모 클래스에 있는 스크롤 기능 , 한 줄로 호출
        HandleScroll();

        // 우클릭시 해당 화면으로 이동
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_SELECT);
            PhoneUIController.Instance.ShowScreen(currentIndex + 1);
        }
    }

    // 부모 클래스에서 강제한 시각적 업데이트 로직 (X축으로 패딩만큼 이동)
    protected override void UpdateHighlightVisuals()
    {
        Vector3 newPos = startPosition;
        newPos.x += currentIndex * padding;
        highlight.transform.localPosition = newPos;
    }
}