using System.Collections.Generic;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class StartManager : MonoBehaviour
{
    [Header("Main Buttons")]
    public Button createBtn;
    public Button joinBtn;
    public Button settingBtn;
    public Button quitBtn;

    [Header("닉네임 입력 후 확인 버튼")]
    public Button nicknameConfirmBtn;

    [Header("Panels (Popups)")]
    public GameObject nicknamePanel;        // 닉네임 입력 패널 (최초 노출)
    public GameObject mainPanel;            // 메인 화면 (버전/제목 등)
    public GameObject createPanel;          // 방 만들기 로딩창
    public GameObject joinPanel;            // 방 목록 리스트창
    public GameObject settingsPanel;        // 설정창

    [Header("Input Fields")]
    public TMP_InputField nicknameInput;    // 플레이어 닉네임

    [Header("Join Panel 설정")]
    public Transform sessionListContent;    // Scroll View의 Content 객체
    public GameObject sessionEntryPrefab;   // SessionUIEntry 프리팹

    [Header("Join Panel 전용 UI")]
    public Button refreshBtn;               // 방 목록 새로고침 버튼 (Reset)
    public Button joinPanelBackBtn;         // 조인 패널에서 나가는 버튼

    private void OnEnable()
    {
        if (MultiPlayerSessionManager.Instance != null)
        {
            MultiPlayerSessionManager.Instance.OnSessionListUpdated -= UpdateSessionListUI;
            MultiPlayerSessionManager.Instance.OnSessionListUpdated += UpdateSessionListUI;
        }
    }

    private void OnDisable()
    {
        if (MultiPlayerSessionManager.Instance != null) MultiPlayerSessionManager.Instance.OnSessionListUpdated -= UpdateSessionListUI;
    }

    void Start()
    {
        if (MultiPlayerSessionManager.Instance != null)
        {
            MultiPlayerSessionManager.Instance.OnSessionListUpdated -= UpdateSessionListUI;
            MultiPlayerSessionManager.Instance.OnSessionListUpdated += UpdateSessionListUI;
        }

        ShowPanel(nicknamePanel);

        // 버튼 리스너 연결
        createBtn.onClick.AddListener(() => ShowPanel(createPanel));
        createBtn.onClick.AddListener(OnConfirmCreate);
        joinBtn.onClick.AddListener(OnJoinBtnClicked);
        settingBtn.onClick.AddListener(() => ShowPanel(settingsPanel));
        quitBtn.onClick.AddListener(QuitGame);

        //닉네임 결정 버튼
        nicknameConfirmBtn.onClick.AddListener(OnNicknameConfirm);

        // Join 패널 내부 버튼들
        if (refreshBtn != null) refreshBtn.onClick.AddListener(OnRefreshClicked); // 새로고침(Reset) 버튼

        if (joinPanelBackBtn != null) joinPanelBackBtn.onClick.AddListener(OnBackToMain); // 뒤로가기 버튼
    }

    // 특정 패널만 켜고 나머지는 끄는 공통 함수
    public void ShowPanel(GameObject targetPanel)
    {
        nicknamePanel.SetActive(targetPanel == nicknamePanel);
        mainPanel.SetActive(targetPanel == mainPanel);
        createPanel.SetActive(targetPanel == createPanel);
        joinPanel.SetActive(targetPanel == joinPanel);
        settingsPanel.SetActive(targetPanel == settingsPanel);
    }

    // 닉네임 입력 후 '확인' 버튼을 누를 때 호출 (UI에서 연결 필요)
    public void OnNicknameConfirm()
    {
        if (string.IsNullOrWhiteSpace(nicknameInput.text))
        {
            Debug.LogWarning("닉네임을 입력해주세요!");
            return;
        }

        // 세션 매니저에 닉네임 저장 (이후 게임 내내 사용됨)
        MultiPlayerSessionManager.Instance.PlayerNickname = nicknameInput.text;

        // 메인 패널로 전환
        ShowPanel(mainPanel);
    }

    // '방 만들기' 확인 버튼에서 호출할 함수 (실제 멀티 연결)
    public void OnConfirmCreate()
    {
        string roomName = $"{MultiPlayerSessionManager.Instance.PlayerNickname}'s Room";

        Debug.Log($"방 생성 시도: {roomName}");

        MultiPlayerSessionManager.Instance.CreateSessionAsync(roomName);

        // 세션 매니저에 데이터 전달 및 방 생성 시작
        //MultiPlayerSessionManager.Instance.PlayerNickname = nicknameInput.text;
    }

    // '방 찾기' 버튼 클릭 시
    private void OnJoinBtnClicked()
    {
        ShowPanel(joinPanel);

        // 서버에서 방 목록을 요청함
        //MultiPlayerSessionManager.Instance.QuerySessionsAsync();
        RefreshSessionList(); // 패널을 열 때 목록을 새로고침함
    }

    // 2. '새로고침(Reset)' 버튼 클릭 시 (Join 패널 내부)
    private void OnRefreshClicked()
    {
        Debug.Log("방 목록 새로고침 요청...");
        RefreshSessionList();
    }

    // 공통 새로고침 로직
    private void RefreshSessionList()
    {
        // 시각적으로 목록이 비워지는 피드백을 주려면 여기서 미리 삭제할 수 있습니다.
        ClearSessionListUI();

        // 서버에 최신 목록 요청 (성공 시 OnSessionListUpdated 이벤트가 발생하여 UpdateSessionListUI 실행)
        if (MultiPlayerSessionManager.Instance != null)
        {
            MultiPlayerSessionManager.Instance.QuerySessionsAsync();
        }
    }

    // UI 리스트만 비우는 헬퍼 함수
    private void ClearSessionListUI()
    {
        foreach (Transform child in sessionListContent)
        {
            Destroy(child.gameObject);
        }
    }

    private void UpdateSessionListUI(List<ISessionInfo> sessions)
    {
        // 1. 기존 리스트 청소
        ClearSessionListUI();

        if (sessions == null || sessions.Count == 0) return;

        // 2. 새로운 방 목록 생성
        foreach (var session in sessions)
        {
            GameObject entryGo = Instantiate(sessionEntryPrefab, sessionListContent);
            print("프리팹 생성하는가");
            //entryGo.transform.localScale = Vector3.one;                                 // 스케일 강제 고정
            //entryGo.transform.localPosition = Vector3.zero;                             // 위치 초기화
            // UI 프리팹이 깨지는 것을 방지하기 위한 코드
            RectTransform rt = entryGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.anchoredPosition3D = Vector3.zero;
            }

            if (entryGo.TryGetComponent<SessionUIEntry>(out var entryScript))
            {
                // 생성 시점에 "클릭하면 이 함수를 실행해라"라고 주입 (의존성 주입)
                entryScript.Setup(session, (selectedSession) => {
                    Debug.Log($"{selectedSession.Name} 선택됨 -> 입장 시도");
                    MultiPlayerSessionManager.Instance.JoinSessionAsync(selectedSession);
                });
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(sessionListContent.GetComponent<RectTransform>());
    }

    public void OnBackToMain()
    {
        ShowPanel(mainPanel);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}