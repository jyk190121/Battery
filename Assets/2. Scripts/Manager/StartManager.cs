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
    public GameObject joinLoadingPanel;     // 방 입장 로딩창
    public GameObject settingsPanel;        // 설정창

    [Header("Input Fields")]
    public TMP_InputField nicknameInput;    // 플레이어 닉네임

    [Header("Join Panel 설정")]
    public Transform sessionListContent;    // Scroll View의 Content 객체
    public GameObject sessionEntryPrefab;   // SessionUIEntry 프리팹

    [Header("Join Panel 전용 UI")]
    public Button refreshBtn;               // 방 목록 새로고침 버튼 (Reset)
    public Button joinPanelBackBtn;         // 조인 패널에서 나가는 버튼

    [Header("취소 버튼")]
    public Button createCancelBtn;
    public Button joinCancelBtn;

    [Header("설정창 닫기 버튼")]
    public Button closeSettingBtn;
    
    bool isCancelling = false;

    void OnEnable()
    {
        TrySubscribeEvents();
    }

    void OnDisable()
    {
        if (MultiPlayerSessionManager.Instance != null) MultiPlayerSessionManager.Instance.OnSessionListUpdated -= UpdateSessionListUI;
    }

    void Start()
    {

        TrySubscribeEvents();

        // --- 닉네임 입력 제한 설정 추가 ---
        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = 12; // 인스펙터에서도 설정 가능하지만 코드로 강제
            nicknameInput.onValueChanged.AddListener(OnNicknameValueChanged);
        }

        // 초기 버튼 상태 설정 (비어있으므로 비활성화)
        nicknameConfirmBtn.interactable = false;

        ShowPanel(nicknamePanel);

        // 버튼 리스너 연결
        createBtn.onClick.AddListener(() => ShowPanel(createPanel));
        createBtn.onClick.AddListener(OnConfirmCreate);
        joinBtn.onClick.AddListener(OnJoinBtnClicked);
        settingBtn.onClick.AddListener(() => ShowPanel(settingsPanel));
        quitBtn.onClick.AddListener(QuitGame);

        if (createCancelBtn != null) createCancelBtn.onClick.AddListener(OnCancelCreate);
        if (joinCancelBtn != null) joinCancelBtn.onClick.AddListener(OnCancelJoin);

        //닉네임 결정 버튼
        nicknameConfirmBtn.onClick.AddListener(OnNicknameConfirm);

        // Join 패널 내부 버튼들
        if (refreshBtn != null) refreshBtn.onClick.AddListener(OnRefreshClicked); // 새로고침(Reset) 버튼

        if (joinPanelBackBtn != null) joinPanelBackBtn.onClick.AddListener(OnBackToMain); // 뒤로가기 버튼

        if (closeSettingBtn != null) closeSettingBtn.onClick.AddListener(() => ShowPanel(mainPanel));
    }
    void TrySubscribeEvents()
    {
        if (MultiPlayerSessionManager.Instance != null)
        {
            MultiPlayerSessionManager.Instance.OnSessionListUpdated -= UpdateSessionListUI;
            MultiPlayerSessionManager.Instance.OnSessionListUpdated += UpdateSessionListUI;
        }
    }
    public void OnCancelCreate()
    {
        //Debug.Log("방 생성 취소");

        //// 실제 네트워크 비동기 작업 중단 요청
        //if (MultiPlayerSessionManager.Instance != null)
        //{
        //    MultiPlayerSessionManager.Instance.CancelSessionOperations();
        //}

        //ClearSessionListUI();

        //ShowPanel(mainPanel);

        Debug.Log("방 생성 취소 시작");
        isCancelling = true; // [추가] 취소 플래그 On

        // 1. 네트워크 및 세션 중단
        if (MultiPlayerSessionManager.Instance != null)
        {
            MultiPlayerSessionManager.Instance.CancelSessionOperations();
        }

        // 2. UI 즉시 파괴
        ClearSessionListUI();

        // 3. 패널 전환
        ShowPanel(mainPanel);

        // 4. [추가] 1초 후 플래그 해제 (비동기 응답이 모두 끝날 시간 벌기)
        Invoke(nameof(ResetCancelFlag), 1.0f);
    }
    void ResetCancelFlag() => isCancelling = false;

    // 방 참가 취소: 로딩 패널을 끄고 조인 패널(목록창)로 이동
    public void OnCancelJoin()
    {
        Debug.Log("방 입장 취소");

        if (MultiPlayerSessionManager.Instance != null)
        {
            MultiPlayerSessionManager.Instance.CancelSessionOperations();
        }

        // 입장 로딩 패널 비활성화
        joinLoadingPanel.SetActive(false);

        // 조인 패널(방 목록)은 유지하거나 다시 띄움
        ShowPanel(joinPanel);

        // 목록 새로고침 (혹시 모를 상태 동기화)
        RefreshSessionList();
    }

    // 특정 패널만 켜고 나머지는 끄는 공통 함수
    public void ShowPanel(GameObject targetPanel)
    {
        nicknamePanel.SetActive(targetPanel == nicknamePanel);
        mainPanel.SetActive(targetPanel == mainPanel);
        createPanel.SetActive(targetPanel == createPanel);
        joinPanel.SetActive(targetPanel == joinPanel);
        settingsPanel.SetActive(targetPanel == settingsPanel);

        joinLoadingPanel.SetActive(false);
    }

    // 닉네임 입력 후 '확인' 버튼을 누를 때 호출 (UI에서 연결 필요)
    public void OnNicknameConfirm()
    {
        // 한 번 더 검증 (버튼이 활성화된 상태라 하더라도 안전을 위해)
        if (string.IsNullOrWhiteSpace(nicknameInput.text) || nicknameInput.text.Length > 12)
        {
            Debug.LogWarning("닉네임 형식이 올바르지 않습니다.");
            return;
        }

        // 세션 매니저에 닉네임 저장 (이후 게임 내내 사용됨)
        // 닉네임 뒤에 랜덤 태그 붙이기 (예: Player#1234) - 중복 방지용
        int randomTag = UnityEngine.Random.Range(1000, 10000);
        MultiPlayerSessionManager.Instance.PlayerNickname = $"{nicknameInput.text}#{randomTag}";

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
        //foreach (Transform child in sessionListContent)
        //{
        //    //Destroy 대신 (즉각적인 UI 갱신을 위해)
        //    DestroyImmediate(child.gameObject);
        //}

        //print("파괴 처리하고 있나");

        if (sessionListContent == null) return;

        // 자식들을 임시 리스트에 담아 안전하게 파괴
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in sessionListContent)
        {
            toDestroy.Add(child.gameObject);
        }

        foreach (GameObject go in toDestroy)
        {
            // 부모 관계를 끊어서 즉시 리스트에서 이탈시킴
            go.transform.SetParent(null);
            DestroyImmediate(go);
        }

        Debug.Log("[UI] 모든 세션 프리팹 파괴 완료");
    }

    void UpdateSessionListUI(List<ISessionInfo> sessions)
    {
        //if (isCancelling || !joinPanel.activeInHierarchy || mainPanel.activeInHierarchy)
        //{
        //    Debug.Log("취소 중이거나 비활성 상태이므로 UI 업데이트를 무시합니다.");
        //    ClearSessionListUI();
        //    return;
        //}

        // [방어막] UI가 목록을 보여줄 상황이 아니면 무조건 리턴
        if (isCancelling || !joinPanel.activeInHierarchy || mainPanel.activeInHierarchy)
        {
            ClearSessionListUI();
            return;
        }

        ClearSessionListUI();

        if (sessions == null || sessions.Count == 0) return;

        // 2. 새로운 방 목록 생성
        foreach (var session in sessions)
        {
            GameObject entryGo = Instantiate(sessionEntryPrefab, sessionListContent);
            //print("프리팹 생성하는가");
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
                entryScript.Setup(session, (selectedSession) => 
                {
                    Debug.Log($"{selectedSession.Name} 선택됨 -> 입장 시도");
                    joinLoadingPanel.SetActive(true);
                    MultiPlayerSessionManager.Instance.JoinSessionAsync(selectedSession);
                });
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(sessionListContent.GetComponent<RectTransform>());
    }

    // 닉네임 입력값이 변할 때마다 호출되는 함수
    private void OnNicknameValueChanged(string input)
    {
        // 공백 제외 1글자 이상 12글자 이하인지 체크
        // (characterLimit으로 상한선은 이미 막혀있으므로 하한선과 공백 위주 체크)
        bool isValid = !string.IsNullOrWhiteSpace(input) && input.Length >= 1 && input.Length <= 12;

        // 조건에 맞을 때만 확인 버튼 활성화
        nicknameConfirmBtn.interactable = isValid;
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