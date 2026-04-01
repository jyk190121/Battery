using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class CameraUI : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("전면(셀카) 모드 일반 카메라")]
    public Camera frontCamera;
    [Tooltip("후면(풍경) 모드 일반 카메라")]
    public Camera backCamera;
    [Tooltip("UI 레이어가 제외된 캡처 전용 카메라")]
    public Camera captureCamera;

    [Header("Capture Settings")]
    public int photoWidth = 1920;
    public int photoHeight = 1080;

    private PhoneUIController phoneUIController;
    private bool isFrontMode = false;
    private bool isCapturing = false;

    public TextMeshProUGUI modeText;

    public GameObject WarningPopup;

    // 갤러리 앱과 연동하기 위한 이벤트
    public static event Action<string> OnPhotoSaved;

    private void Awake()
    {
        phoneUIController = FindAnyObjectByType<PhoneUIController>();

        // 사진이 저장될 Photos 폴더 생성
        string folderPath = Path.Combine(Application.persistentDataPath, "Photos");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 캡처 카메라는 평소에 렌더링하지 않도록 컴포넌트를 꺼둡니다.
        if (captureCamera != null) captureCamera.enabled = false;
    }

    private void OnEnable()
    {
        // 카메라 앱이 켜질 때 기본 모드 설정 (후면 카메라 활성화)
        isFrontMode = false;
        UpdateCameraState();

        WarningPopup.SetActive(false);
    }

    private void OnDisable()
    {
        // 앱이 꺼질 때 폰 카메라 렌더링을 중지하기 위해 컴포넌트 비활성화
        if (frontCamera != null) frontCamera.enabled = false;
        if (backCamera != null) backCamera.enabled = false;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // 카메라 앱 종료
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            phoneUIController.ShowScreen(0);
            return;
        }

        if (Mouse.current == null || isCapturing) return;

        HandleCameraToggle();
        HandleCapture();
    }

    private void HandleCameraToggle()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (scrollY > 0 && isFrontMode) // 마우스 휠 위로: 후면 카메라
        {
            isFrontMode = false;
            UpdateCameraState();
        }
        else if (scrollY < 0 && !isFrontMode) // 마우스 휠 아래로: 전면 카메라
        {
            isFrontMode = true;
            UpdateCameraState();
        }
    }

    private void UpdateCameraState()
    {
        // GameObject.SetActive 대신 Camera.enabled를 사용하여 RenderTexture 갱신 딜레이 및 버그 방지
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
    }

    private void HandleCapture()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            // 물리적 폴더 대신 인게임 명부의 사진 개수(최대 4장)를 검사합니다.
            if (GalleryUI.currentCyclePhotos.Count >= 4)
            {
                StartCoroutine(ShowWarningPopup());
                return;
            }

            StartCoroutine(CapturePhotoRoutine());
        }
    }

    private IEnumerator CapturePhotoRoutine()
    {
        isCapturing = true;

        // 프레임 렌더링이 완료될 때까지 대기
        yield return new WaitForEndOfFrame();

        // 1. 현재 활성화된 카메라 확인
        Camera activeCamera = isFrontMode ? frontCamera : backCamera;

        // 2. 캡처 카메라의 위치와 회전값을 현재 바라보고 있는 카메라와 일치시킴
        if (activeCamera != null && captureCamera != null)
        {
            captureCamera.transform.position = activeCamera.transform.position;
            captureCamera.transform.rotation = activeCamera.transform.rotation;
            captureCamera.fieldOfView = activeCamera.fieldOfView;
        }

        // 캡처용 RenderTexture 생성 및 카메라에 할당
        RenderTexture rt = new RenderTexture(photoWidth, photoHeight, 24);
        captureCamera.targetTexture = rt;

        // 캡처 카메라 강제 렌더링
        captureCamera.Render();

        // RenderTexture에서 Texture2D로 픽셀 데이터 읽기
        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, photoWidth, photoHeight), 0, 0);
        screenShot.Apply();

        // 메모리 정리
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        // 파일명 생성 및 실제 PC에 저장
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"IMG_{timestamp}.png";
        string filePath = Path.Combine(Application.persistentDataPath, "Photos", fileName);

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        Destroy(screenShot);

        // 실제 저장이 끝난 후, 이번 사이클 명부에 해당 사진의 경로를 등록합니다.
        GalleryUI.currentCyclePhotos.Add(filePath);

        // 갤러리 앱을 위한 이벤트 발송
        OnPhotoSaved?.Invoke(filePath);
        Debug.Log($"[CameraUI] 찰칵! 사진 저장 및 명부 등록 완료: {filePath}");

        isCapturing = false;
    }

    private IEnumerator ShowWarningPopup()
    {
        WarningPopup.SetActive(true);
        yield return new WaitForSeconds(1f);

        WarningPopup.SetActive(false);
    }
}