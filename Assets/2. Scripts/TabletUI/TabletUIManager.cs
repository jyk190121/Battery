using UnityEngine;
using UnityEngine.InputSystem;
using System; // Action 사용을 위해 추가

public class TabletUIManager : MonoBehaviour
{
    [Header("Camera & RenderTexture Settings")]
    public Camera uiCamera;
    public RenderTexture tvRenderTexture;

    // [핵심 추가] 태블릿 상태가 변할 때마다 신호를 보낼 이벤트
    public static event Action<bool> OnTabletStateChanged;

    private bool isTabletOpen = false;
    private PlayerController currentPlayerController;
    private Canvas playerHudCanvas;

    private void Start()
    {
        CloseTabletUI();
    }

    private void Update()
    {
        if (isTabletOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTabletUI();
        }
    }

    public void OpenTabletUI(PlayerController player)
    {
        if (isTabletOpen) return;

        isTabletOpen = true;
        currentPlayerController = player;

        // 1. 이벤트 발송: 태블릿이 열렸음을 구독자들에게 알림 (단 1회 실행)
        OnTabletStateChanged?.Invoke(true);

        // 2. 렌더텍스처 해제 및 팝업 설정
        uiCamera.targetTexture = null;
        uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0.7f);

        // 3. 메인 HUD 캔버스 끄기 (PlayerUIManager 활용)
        if (PlayerUIManager.LocalInstance != null && PlayerUIManager.LocalInstance.playerHpImage != null)
        {
            playerHudCanvas = PlayerUIManager.LocalInstance.playerHpImage.GetComponentInParent<Canvas>();
            if (playerHudCanvas != null) playerHudCanvas.enabled = false;
        }

        // 4. E키 상호작용 UI 강제 끄기
        if (player.Interaction != null && player.Interaction.interactUI != null)
        {
            player.Interaction.interactUI.SetActive(false);
        }

        // 5. 캐릭터 이동 제한
        if (player.TryGetComponent(out PlayerMove move))
        {
            move.SetControlLock(true);
        }
    }

    public void CloseTabletUI()
    {
        if (!isTabletOpen) return;

        isTabletOpen = false;

        // 1. 이벤트 발송: 태블릿이 닫혔음을 구독자들에게 알림 (단 1회 실행)
        OnTabletStateChanged?.Invoke(false);

        // 2. 렌더텍스처 및 화면 원복
        if (uiCamera != null)
        {
            uiCamera.targetTexture = tvRenderTexture;
            uiCamera.backgroundColor = Color.black;
        }

        // 3. 메인 HUD 캔버스 다시 켜기
        if (playerHudCanvas != null)
        {
            playerHudCanvas.enabled = true;
        }

        // 4. 캐릭터 이동 제한 해제
        if (currentPlayerController != null)
        {
            if (currentPlayerController.TryGetComponent(out PlayerMove move))
            {
                move.SetControlLock(false);
            }
        }
        currentPlayerController = null;
    }
}