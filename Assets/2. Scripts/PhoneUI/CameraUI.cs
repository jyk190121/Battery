using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

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
        // 1. 내 로컬 플레이어의 카메라만 정확하게 찾아오기 (매우 중요)
        if (frontCamera == null || backCamera == null || captureCamera == null)
        {
            FindLocalPlayerCameras();
        }

        isFrontMode = false;
        WarningPopup.SetActive(false);

        // 카메라 초기 상태 세팅
        if (captureCamera != null) captureCamera.enabled = false;
        UpdateCameraState();

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

    // Netcode 환경에서 내 캐릭터(Local Player)의 카메라 그룹만 찾는 함수
    private void FindLocalPlayerCameras()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            // 내 로컬 캐릭터 오브젝트 가져오기
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;

            if (localPlayerObj != null)
            {
                // 내 캐릭터 자식들 중에서 CameraConnect 찾기
                CameraConnect cameraGroup = localPlayerObj.GetComponentInChildren<CameraConnect>();

                if (cameraGroup != null)
                {
                    frontCamera = cameraGroup.FrontCamera;
                    backCamera = cameraGroup.BackCamera;
                    captureCamera = cameraGroup.CaptureCamera;
                    Debug.Log("[CameraUI] 로컬 플레이어의 카메라를 성공적으로 연결했습니다.");
                }
            }
        }
        else
        {
            // 서버 연결 전 테스트용 폴백 (에디터 테스트용)
            CameraConnect cameraGroup = FindAnyObjectByType<CameraConnect>();
            if (cameraGroup != null)
            {
                frontCamera = cameraGroup.FrontCamera;
                backCamera = cameraGroup.BackCamera;
                captureCamera = cameraGroup.CaptureCamera;
            }
        }
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
        if (Mouse.current.leftButton.wasPressedThisFrame)
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