using UnityEngine;
using UnityEngine.InputSystem;
using System; // Action 이벤트 사용을 위해 필요

public class TabletUIManager : MonoBehaviour
{
    // 외부에서 즉시 확인할 수 있는 정적 변수 (태블릿 On/Off 확인용)
    public static bool IsAnyTabletOpen { get; private set; } = false;

    [Header("Camera & RenderTexture Settings")]
    public Camera uiCamera;
    public RenderTexture tvRenderTexture;

    // 태블릿 상태 변화를 외부(PlayerRotation 등)에 알리기 위한 정적 이벤트
    public static event Action<bool> OnTabletStateChanged;

    //private bool isTabletOpen = false;
    //private PlayerController currentPlayerController;
    private Canvas playerHudCanvas; // 플레이어의 메인 HUD 캔버스 참조 저장용

    private void Start()
    {
        // 게임 시작 시 초기 상태 설정 (TV 송출 모드)
        CloseTabletUI();
    }

    private void Update()
    {
        // 태블릿이 열려있을 때 ESC 키를 누르면 닫기
        if (IsAnyTabletOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTabletUI();
        }
    }

    /// <summary>
    /// 태블릿을 열고 조작 모드로 전환 (PlayerInteraction에서 호출)
    /// </summary>
    public void OpenTabletUI(PlayerController player)
    {
        if (IsAnyTabletOpen) return;

        //isTabletOpen = true;
        IsAnyTabletOpen = true; // 정적 변수 업데이트
        OnTabletStateChanged?.Invoke(true);
        //currentPlayerController = player;

        // 1. 이벤트 발송: 구독 중인 스크립트(PlayerRotation 등)에 알림
        OnTabletStateChanged?.Invoke(true);

        // 2. 카메라 출력을 모니터 화면으로 변경 (2D 팝업 활성화)
        uiCamera.targetTexture = null;
        uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0.7f); // 배경 반투명 처리

        // 3. 플레이어 HUD 캔버스 비활성화 (시야 확보 및 최적화)
        if (PlayerUIManager.LocalInstance != null && PlayerUIManager.LocalInstance.playerHpImage != null)
        {
            playerHudCanvas = PlayerUIManager.LocalInstance.playerHpImage.GetComponentInParent<Canvas>();
            if (playerHudCanvas != null) playerHudCanvas.enabled = false;
        }

        // 4. 상호작용 텍스트 UI 강제 비활성화
        if (player.Interaction != null && player.Interaction.interactUI != null)
        {
            player.Interaction.interactUI.SetActive(false);
        }

        //// 5. 캐릭터 조작 잠금 (이동 및 공격 방지)
        //if (player.TryGetComponent(out PlayerMove move))
        //{
        //    move.SetControlLock(true);
        //}
    }

    /// <summary>
    /// 태블릿을 닫고 다시 TV 송출 모드로 전환
    /// </summary>
    public void CloseTabletUI()
    {
        if (!IsAnyTabletOpen) return;

        //IsAnyTabletOpen = false;
        IsAnyTabletOpen = false; // 정적 변수 업데이트
        OnTabletStateChanged?.Invoke(false);

        // 1. 이벤트 발송: 구독 중인 스크립트에 알림
        OnTabletStateChanged?.Invoke(false);

        // 2. 카메라 출력을 다시 RenderTexture로 변경 (TV 화면 송출)
        if (uiCamera != null)
        {
            uiCamera.targetTexture = tvRenderTexture;
            uiCamera.backgroundColor = Color.black; // 기본 배경색으로 복구
        }

        // 3. 플레이어 HUD 캔버스 다시 활성화
        if (playerHudCanvas != null)
        {
            playerHudCanvas.enabled = true;
        }

        // 4. 캐릭터 조작 잠금 해제
        //if (currentPlayerController != null)
        //{
        //    if (currentPlayerController.TryGetComponent(out PlayerMove move))
        //    {
        //        move.SetControlLock(false);
        //    }
        //}

        //currentPlayerController = null;
    }
}