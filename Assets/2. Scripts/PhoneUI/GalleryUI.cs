using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GalleryUI : MonoBehaviour
{
    [Header("UI Components")]
    public RawImage mainDisplay;
    public RawImage[] thumbnails;
    public RectTransform highlight;

    [Header("Gallery Settings")]
    public float padding = 100f;
    public float deleteHoldTime = 1.5f;

    private PhoneUIController phoneUIController;
    private int currentIndex = 0;
    private readonly int maxIndex = 3;
    private Vector3 startPosition;

    // 물리적 폴더 대신 사용할 이번 사이클 전용 사진 명부
    public static List<string> currentCyclePhotos = new List<string>();

    private Texture2D[] loadedTextures = new Texture2D[4];

    private bool isHoldingRightClick = false;
    private float currentHoldTime = 0f;
    public GameObject deletePopup;
    public Image deleteGauge;

    private bool isRightClickBlocked = true;

    private void Awake()
    {
        phoneUIController = FindAnyObjectByType<PhoneUIController>();
        if (highlight != null)
        {
            startPosition = highlight.localPosition;
        }
    }

    private void OnEnable()
    {
        currentIndex = 0;
        LoadPhotos();
        UpdateHighlightPosition();
        UpdateMainDisplay();

        isHoldingRightClick = false;
        currentHoldTime = 0f;
        if (deletePopup != null) deletePopup.SetActive(false);
        if (deleteGauge != null) deleteGauge.fillAmount = 0f;

        isRightClickBlocked = true;
    }

    private void OnDisable()
    {
        ClearLoadedTextures();

        isHoldingRightClick = false;
        currentHoldTime = 0f;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            phoneUIController.ShowScreen(0);
            return;
        }

        HandleScroll();
        HandleDelete();

        // 기능 확인용: F1을 누르면 현재 사이클 명부를 초기화하고 화면을 갱신합니다.
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            ClearCyclePhotos();
            LoadPhotos();
            UpdateMainDisplay();
        }
    }

    #region 데이터 로드 및 관리
    private void LoadPhotos()
    {
        ClearLoadedTextures();

        // 폴더를 뒤지지 않고, currentCyclePhotos 명부를 기반으로 텍스처를 로드합니다.
        for (int i = 0; i < thumbnails.Length; i++)
        {
            if (i < currentCyclePhotos.Count)
            {
                string path = currentCyclePhotos[i];
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(bytes);
                    loadedTextures[i] = texture;

                    thumbnails[i].texture = texture;
                    thumbnails[i].gameObject.SetActive(true);
                }
            }
            else
            {
                thumbnails[i].texture = null;
                thumbnails[i].gameObject.SetActive(false);
            }
        }
    }

    private void ClearLoadedTextures()
    {
        for (int i = 0; i < loadedTextures.Length; i++)
        {
            if (loadedTextures[i] != null)
            {
                Destroy(loadedTextures[i]);
                loadedTextures[i] = null;
            }
        }
    }
    #endregion

    #region 조작 및 UI 업데이트
    private void HandleScroll()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (scrollY != 0)
        {
            if (scrollY > 0) MoveHighlight(-1);
            else if (scrollY < 0) MoveHighlight(1);
        }
    }

    private void MoveHighlight(int direction)
    {
        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, maxIndex);

        if (nextIndex != currentIndex)
        {
            currentIndex = nextIndex;
            UpdateHighlightPosition();
            UpdateMainDisplay();

            isHoldingRightClick = false;
            currentHoldTime = 0f;
        }
    }

    private void UpdateHighlightPosition()
    {
        Vector3 newPos = startPosition;
        newPos.x += currentIndex * padding;
        highlight.localPosition = newPos;
    }

    private void UpdateMainDisplay()
    {
        if (currentIndex < currentCyclePhotos.Count && loadedTextures[currentIndex] != null)
        {
            mainDisplay.texture = loadedTextures[currentIndex];
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
        if (isRightClickBlocked)
        {
            if (!Mouse.current.rightButton.isPressed)
            {
                isRightClickBlocked = false;
            }
            return;
        }

        if (currentIndex >= currentCyclePhotos.Count) return;

        if (Mouse.current.rightButton.isPressed)
        {
            isHoldingRightClick = true;
            deletePopup.SetActive(true);
            currentHoldTime += Time.deltaTime;

            float progress = currentHoldTime / deleteHoldTime;
            deleteGauge.fillAmount = Mathf.Clamp01(progress);

            if (currentHoldTime >= deleteHoldTime)
            {
                DeleteCurrentPhoto();

                isHoldingRightClick = false;
                currentHoldTime = 0f;

                deleteGauge.fillAmount = 0f;
                deletePopup.SetActive(false);
            }
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            if (isHoldingRightClick)
            {
                isHoldingRightClick = false;
                currentHoldTime = 0f;

                deleteGauge.fillAmount = 0f;
                deletePopup.SetActive(false);
            }
        }
    }

    private void DeleteCurrentPhoto()
    {
        // File.Delete를 쓰지 않고, 명부(List)에서만 항목을 제거합니다.
        if (currentIndex < currentCyclePhotos.Count)
        {
            string pathToRemove = currentCyclePhotos[currentIndex];
            currentCyclePhotos.RemoveAt(currentIndex);
            Debug.Log($"[GalleryUI] 인게임 명부에서 사진 제외 완료 (실제 파일 유지): {pathToRemove}");

            LoadPhotos();
            UpdateMainDisplay();
        }
    }
    #endregion

    #region 사이클 초기화
    // 사이클 종료 시 폴더의 파일을 지우는 대신 명부만 백지화합니다.
    public static void ClearCyclePhotos()
    {
        currentCyclePhotos.Clear();
        Debug.Log("[GalleryUI] 사이클 종료: 현재 회차의 사진 명부가 초기화되었습니다. (실제 파일은 보존됨)");
    }
    #endregion
}