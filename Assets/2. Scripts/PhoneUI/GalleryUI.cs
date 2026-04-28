using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GalleryUI : ScrollSelectionUI
{
    [Header("UI Components")]
    public RawImage mainDisplay;
    public RawImage[] thumbnails;
    public RectTransform highlight;

    [Header("Gallery Settings")]
    public float padding = 100f;
    public float deleteHoldTime = 1.5f;

    private Vector3 startPosition;

    private bool isHoldingLeftClick = false;
    private float currentHoldTime = 0f;
    public GameObject deletePopup;
    public Image deleteGauge;

    private bool isLeftClickBlocked = true;

    private void Awake()
    {
        if (highlight != null)
        {
            startPosition = highlight.localPosition;
        }
        maxIndex = 3;
    }

    private void OnEnable()
    {
        currentIndex = 0;
        LoadPhotos();
        UpdateHighlightVisuals();
        UpdateMainDisplay();

        isHoldingLeftClick = false;
        currentHoldTime = 0f;
        if (deletePopup != null) deletePopup.SetActive(false);
        if (deleteGauge != null) deleteGauge.fillAmount = 0f;

        isLeftClickBlocked = true;

        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
    }

    private void OnDisable()
    {
        isHoldingLeftClick = false;
        currentHoldTime = 0f;

        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        HandleScroll();
        HandleDelete();

        // F1키 사이클 초기화 (매니저 호출) 테스트용    - 실제 게임에서는 사이클 종료 시점에 자동으로 호출되어야 함
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            PhotoDataManager.Instance.ClearAllPhotos();
            LoadPhotos();
            UpdateMainDisplay();
        }
    }

    private void HandleBack()
    {
        PhoneUIController.Instance.ShowScreen(0);
    }

    protected override void UpdateHighlightVisuals()
    {
        Vector3 newPos = startPosition;
        newPos.x += currentIndex * padding;
        highlight.localPosition = newPos;
    }

    protected override void OnIndexChanged()
    {
        UpdateMainDisplay();
        isHoldingLeftClick = false;
        currentHoldTime = 0f;
    }

    #region 데이터 로드 및 관리
    private void LoadPhotos()
    {
        // 매니저에서 사진을 직접 가져옴
        var photos = PhotoDataManager.Instance.currentPhotos;

        for (int i = 0; i < thumbnails.Length; i++)
        {
            if (i < photos.Count)
            {
                thumbnails[i].texture = photos[i].image;
                thumbnails[i].gameObject.SetActive(true);
            }
            else
            {
                thumbnails[i].texture = null;
                thumbnails[i].gameObject.SetActive(false);
            }
        }
    }

    private void UpdateMainDisplay()
    {
        var photos = PhotoDataManager.Instance.currentPhotos;

        if (currentIndex < photos.Count && photos[currentIndex] != null)
        {
            mainDisplay.texture = photos[currentIndex].image;
            mainDisplay.gameObject.SetActive(true);
        }
        else
        {
            mainDisplay.texture = null;
            mainDisplay.gameObject.SetActive(false);
        }
    }
    #endregion

    #region 우클릭 삭제 (Hold to Delete)
    private void HandleDelete()
    {
        if (isLeftClickBlocked)
        {
            if (!Mouse.current.leftButton.isPressed) isLeftClickBlocked = false;
            return;
        }

        if (currentIndex >= PhotoDataManager.Instance.currentPhotos.Count) return;

        if (Mouse.current.leftButton.isPressed)
        {
            if (!isHoldingLeftClick)
            {
                // 누르기 시작할 때 삭제 게이지 소리 
                SoundManager.Instance.PlaySfx(SfxSound.PHONE_GALLERYDELETE);
            }

            isHoldingLeftClick = true;
            deletePopup.SetActive(true);
            currentHoldTime += Time.deltaTime;

            float progress = currentHoldTime / deleteHoldTime;
            deleteGauge.fillAmount = Mathf.Clamp01(progress);

            if (currentHoldTime >= deleteHoldTime)
            {
                DeleteCurrentPhoto();

                isHoldingLeftClick = false;
                currentHoldTime = 0f;

                deleteGauge.fillAmount = 0f;
                deletePopup.SetActive(false);
            }
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (isHoldingLeftClick)
            {
                isHoldingLeftClick = false;
                currentHoldTime = 0f;

                deleteGauge.fillAmount = 0f;
                deletePopup.SetActive(false);
            }
        }
    }

    private void DeleteCurrentPhoto()
    {
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_GALLERYDELETED);

        // 매니저를 통해 사진(과 데이터) 완전히 삭제
        PhotoDataManager.Instance.RemovePhoto(currentIndex);
        Debug.Log($"[GalleryUI] {currentIndex}번 사진 및 데이터 삭제 완료");

        if (QuestCameraBridge.Instance != null)
        {
            QuestCameraBridge.Instance.RecalculateLocalDeferredQuests();
        }

        LoadPhotos();
        UpdateMainDisplay();
    }
    #endregion
}