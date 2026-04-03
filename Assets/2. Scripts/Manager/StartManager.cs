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
        // 세션 매니저의 이벤트 구독
        if (MultiPlayerSessionManager.Instance != null)
            MultiPlayerSessionManager.Instance.OnSessionListUpdated += UpdateSessionListUI;
    }

    private void OnDisable()
    {
        if (MultiPlayerSessionManager.Instance != null)
            MultiPlayerSessionManager.Instance.OnSessionListUpdated -= UpdateSessionListUI;
    }

    void Start()
    {
        ShowPanel(nicknamePanel);

        // 버튼 리스너 연결
        createBtn.onClick.AddListener(() => ShowPanel(createPanel));
        createBtn.onClick.AddListener(OnConfirmCreate);
        joinBtn.onClick.AddListener(OnJoinBtnClicked);
        settingBtn.onClick.AddListener(() => ShowPanel(settingsPanel));
        quitBtn.onClick.AddListener(QuitGame);

        //닉네임 결정 버튼
        nicknameConfirmBtn.onClick.AddListener(OnNicknameConfirm);
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
        MultiPlayerSessionManager.Instance.QuerySessionsAsync();
    }

    private void UpdateSessionListUI(List<ISession> sessions)
    {
        // 1. 기존 리스트 청소
        foreach (Transform child in sessionListContent)
        {
            Destroy(child.gameObject);
        }

        if (sessions == null || sessions.Count == 0) return;

        // 2. 새로운 방 목록 생성
        foreach (var session in sessions)
        {
            GameObject entryGo = Instantiate(sessionEntryPrefab, sessionListContent);

            if (entryGo.TryGetComponent<SessionUIEntry>(out var entryScript))
            {
                // 생성 시점에 "클릭하면 이 함수를 실행해라"라고 주입 (의존성 주입)
                entryScript.Setup(session, (selectedSession) => {
                    Debug.Log($"{selectedSession.Name} 선택됨 -> 입장 시도");
                    MultiPlayerSessionManager.Instance.JoinSessionAsync(selectedSession);
                });
            }
        }
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