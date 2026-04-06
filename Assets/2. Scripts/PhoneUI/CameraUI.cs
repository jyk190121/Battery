using System;
using System.Collections;
using TMPro;
using Unity.VectorGraphics.Editor;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraUI : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera frontCamera;
    public Camera backCamera;
    public Camera captureCamera;

    [Header("Capture Settings")]
    public int photoWidth = 1920;
    public int photoHeight = 1080;

    private bool isFrontMode = false;
    private bool isCapturing = false;

    public TextMeshProUGUI modeText;
    public GameObject WarningPopup;

    private void Awake()
    {
        if (captureCamera != null) captureCamera.enabled = false;
    }

    private void OnEnable()
    {
        isFrontMode = false;
        UpdateCameraState();
        WarningPopup.SetActive(false);

        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
    }

    private void OnDisable()
    {
        if (frontCamera != null) frontCamera.enabled = false;
        if (backCamera != null) backCamera.enabled = false;

        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || isCapturing) return;

        HandleCameraToggle();
        HandleCapture();
    }

    private void HandleBack()
    {
        PhoneUIController.Instance.ShowScreen(0);
    }

    private void HandleCameraToggle()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (scrollY > 0 && isFrontMode)
        {
            isFrontMode = false;
            UpdateCameraState();
        }
        else if (scrollY < 0 && !isFrontMode)
        {
            isFrontMode = true;
            UpdateCameraState();
        }
    }

    private void UpdateCameraState()
    {
        if (isFrontMode)
        {
            if (backCamera != null) backCamera.enabled = false;
            if (frontCamera != null) frontCamera.enabled = true;
            modeText.text = "Front";
        }
        else
        {
            if (frontCamera != null) frontCamera.enabled = false;
            if (backCamera != null) backCamera.enabled = true;
            modeText.text = "Back";
        }
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_PHOTOMODECHANGE);
    }

    private void HandleCapture()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            // 매니저를 통해 사진 개수 검사
            if (PhotoDataManager.Instance.currentPhotos.Count >= PhotoDataManager.Instance.maxPhotos)
            {
                SoundManager.Instance.PlaySfx(SfxSound.PHONE_PHOTOFULLALERT);
                StartCoroutine(ShowWarningPopup());
                return;
            }
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_TAKEPHOTO);
            StartCoroutine(CapturePhotoRoutine());
        }
    }

    private IEnumerator CapturePhotoRoutine()
    {
        isCapturing = true;
        yield return new WaitForEndOfFrame();

        Camera activeCamera = isFrontMode ? frontCamera : backCamera;

        if (activeCamera != null && captureCamera != null)
        {
            captureCamera.transform.position = activeCamera.transform.position;
            captureCamera.transform.rotation = activeCamera.transform.rotation;
            captureCamera.fieldOfView = activeCamera.fieldOfView;
        }

        RenderTexture rt = new RenderTexture(photoWidth, photoHeight, 24);
        captureCamera.targetTexture = rt;
        captureCamera.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, photoWidth, photoHeight), 0, 0);
        screenShot.Apply();

        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        // =========================================================
        // 기존의 파일 저장 과정 삭제, 메모리 기반 데이터 조립
        // =========================================================
        PhotoData newPhoto = new PhotoData
        {
            image = screenShot,
            // TODO: 나중에 여기에 Physics.Raycast 등을 이용한 판정 함수를 연동하기 -> 퀘스트 판정
            hasMonster = false,     // 예: CheckMonsterInFrame()
            isBrightEnough = true,  // 예: CheckLightLevel()
            hasSpecificItem = false,
            playersInFrame = 1      // 예: CountPlayersInFrame()
        };

        // 매니저에 사진 등록
        PhotoDataManager.Instance.AddPhoto(newPhoto);
        Debug.Log("[CameraUI] 찰칵! 무손실 메모리 저장 및 메타데이터 판정 완료.");

        isCapturing = false;
    }

    private IEnumerator ShowWarningPopup()
    {
        WarningPopup.SetActive(true);
        yield return new WaitForSeconds(1f);
        WarningPopup.SetActive(false);
    }
}