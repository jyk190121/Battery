using TMPro;
using UnityEngine;
using Key = UnityEngine.InputSystem.Key;

public class SettingsUIController : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private GameObject settingsUIPanel; // SettingsUI 프리팹이나 오브젝트

    [Header("홈버튼 / 나가기 버튼")]
    public GameObject closeBtn;
    public GameObject exitBtn;

    bool isSettingsOpen = false;

    public static SettingsUIController Instance;        // 싱글톤 추가
    public bool IsSettingsOpen => isSettingsOpen;       // 외부 참조용 프로퍼티

    void Awake() => Instance = this;
    void Start()
    {
        // 시작 시 설정창은 닫혀 있어야 함
        if (settingsUIPanel != null)
        {
            settingsUIPanel.SetActive(false);
        }
    }

    void Update()
    {
        // ESC 키 입력 감지
        if (Input.GetKeyDown(Key.Escape) && GameSceneManager.Instance.SceneName() != "KJY_TITLE")
        {
            HandleEscapeInput();
        }
    }
    void HandleEscapeInput()
    {
        // 1. 태블릿 UI가 열려 있는지 먼저 확인 (로비 씬 전용 로직)
        // TabletUIManager.Instance가 존재하고, 로컬에서 열려 있는지 확인합니다.
        if (TabletUIManager.Instance != null)
        {
            // TabletUIManager 내부의 isLocalTabletOpen 필드가 private이므로 
            // 공개된 상태 확인 프로퍼티가 없다면 아래와 같이 이벤트를 구독하거나 
            // 점유자(currentTabletUser)를 통해 로컬 플레이어인지 확인합니다.

            // 태블릿 점유자가 나 자신인지 확인
            bool isLocalTabletOpen = IsLocalPlayerUsingTablet();

            if (isLocalTabletOpen)
            {
                // 태블릿이 열려 있다면 태블릿만 닫고 함수 종료 (설정창은 열지 않음)
                Debug.Log("[SettingController] 태블릿이 열려 있어 태블릿을 닫습니다.");
                TabletUIManager.Instance.CloseTabletUI();
                return;
            }
        }

        // 2. 태블릿이 닫혀 있는 상태라면 설정창 토글
        ToggleSettings();
    }
    /// <summary>
    /// 현재 로컬 플레이어가 태블릿을 사용 중인지 판단
    /// </summary>
    bool IsLocalPlayerUsingTablet()
    {
        if (TabletUIManager.Instance == null) return false;

        // TabletUIManager의 currentTabletUser 네트워크 변수를 참조
        // ulong.MaxValue가 아니고, 로컬 플레이어의 ClientId와 같다면 사용 중인 것
        return TabletUIManager.Instance.currentTabletUser.Value != ulong.MaxValue &&
               TabletUIManager.Instance.currentTabletUser.Value == Unity.Netcode.NetworkManager.Singleton.LocalClientId;
    }

    public void ToggleSettings()
    {
        isSettingsOpen = !isSettingsOpen;
        settingsUIPanel.SetActive(isSettingsOpen);

        if (isSettingsOpen) OpenActions();
        else CloseActions();
    }

    private void OpenActions()
    {
        // 1. 마우스 커서 활성화
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string currentScene = GameSceneManager.Instance.SceneName();
        bool isInGameOrLobby = currentScene == "KJY_Lobby" || currentScene == "KJY_Player";

        if (closeBtn != null) closeBtn.SetActive(!isInGameOrLobby);
        if (exitBtn != null) exitBtn.SetActive(isInGameOrLobby);
        SetPlayerInputState(false);
    }

    void CloseActions()
    {
        // 1. 마우스 커서 다시 가두기 (게임 씬인 경우)
        // 로비와 게임 씬의 커서 상태가 다르다면 조건을 추가하세요.
        if (GameSceneManager.Instance.SceneName() == "KJY_Player" ||
            GameSceneManager.Instance.SceneName() == "KJY_Lobby")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // 로비나 타이틀에서는 커서가 보여야 함
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        SetPlayerInputState(true);
    }

    void SetPlayerInputState(bool canInput)
    {
        var localPlayer = Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer != null)
        {
            // PlayerController에 입력 제한 함수가 있다면 호출
            if (localPlayer.TryGetComponent<PlayerController>(out var pc))
            {
                // pc.SetCanMove(canInput); // 예시 함수
            }
        }
    }
}