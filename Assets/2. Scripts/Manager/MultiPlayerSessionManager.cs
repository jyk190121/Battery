using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
// 반드시 필요 (LobbyService.Instance.UpdateLobbyAsync 사용)
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiPlayerSessionManager : NetworkBehaviour
{
    public static MultiPlayerSessionManager Instance { get; private set; }

    public string ExplodedSessionId { get; private set; } = "";

    [Header("설정")]
    private const string LOBBY_SCENE_NAME = "KJY_Lobby";
    private const string START_SCENE_NAME = "KJY_TITLE";

    [Header("매니저 프리팹")]
    public GameObject gameSessionManagerPrefab;
    public GameObject gameManager_ServerPrefab;
    // 현재 활성화된 세션 정보
    public ISession ActiveSession { get; private set; }

    // 포톤에서 가져다 쓸 순수 문자열 ID
    public string CurrentChannelId { get; private set; } = "LobbyChannel";

    // 과도하게 요청 방지용
    bool _isQuerying = false;

    // 로컬 플레이어 닉네임 (UI에서 입력받아 설정)
    private string _playerNickname = null; // 기본값 설정

    public string PlayerNickname
    {
        get => _playerNickname;
        set
        {
            // 공백 체크 등 최소한의 검증 후 저장
            if (!string.IsNullOrWhiteSpace(value)) _playerNickname = value;
        }
    }

    public void SetNickname(string name)
    {
        PlayerNickname = name;
        PlayerPrefs.SetString("SavedNickname", name); // 로컬 기기에 저장
    }

    // UI 갱신을 위한 이벤트 (방 목록 전달)
    //public event Action<List<ISession>> OnSessionListUpdated;
    public event Action<List<ISessionInfo>> OnSessionListUpdated;
    public event Action<bool> OnHostStatusChanged;

    private bool _isLeaving = false;

    // 비동기 작업 취소를 위한 토큰 소스
    CancellationTokenSource _sessionCancelSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private async void Start()
    {
        try
        {
            // 1. 유니티 서비스 초기화
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            // 2. 익명 로그인 (플레이어 고유 ID 확보)
            await EnsureSignedInAsync();

            // 3. 네트워크 매니저 콜백 등록
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"[Multiplayer] 초기화 실패: {e.Message}");
        }
    }
    #region 포톤 서비스 순서 정렬을 위한 로직 추가

    private bool _isVoiceConnecting = false;
    public override void OnNetworkSpawn()
    {
        if (SceneManager.GetActiveScene().name == START_SCENE_NAME)
        {
            PlayerController.AllPlayers.Clear();
            // 씬 로드 시 부모가 생겼다면 Root로 빼줌 (DontDestroyOnLoad 에러 방지)
            transform.SetParent(null);
        }

        _isVoiceConnecting = false;
        //HandleNetcodeConnected(NetworkManager.Singleton.LocalClientId);
        if (IsOwner && SceneManager.GetActiveScene().name != START_SCENE_NAME)
        {
            // 씬 로드가 완전히 끝난 뒤에 연결을 시도하도록 지연
            StartCoroutine(InitializePhotonServicesRoutine());
        }
    }

    IEnumerator InitializePhotonServicesRoutine()
    {
        float timeout = 3.0f;
        while (string.IsNullOrEmpty(CurrentChannelId) || CurrentChannelId == "LobbyChannel")
        {
            if (ActiveSession != null && !string.IsNullOrEmpty(ActiveSession.Id))
            {
                CurrentChannelId = ActiveSession.Id;
                break;
            }

            timeout -= 0.5f;
            if (timeout <= 0) break;
            yield return new WaitForSeconds(0.5f);
        }

        if (GlobalVoiceManager.Instance != null && !string.IsNullOrEmpty(CurrentChannelId))
        {
            Debug.Log($"[Multiplayer] 보이스 연결 시작 - 채널ID: {CurrentChannelId}");
            GlobalVoiceManager.Instance.ConnectVoice(PlayerNickname, CurrentChannelId);
        }
        else
        {
            Debug.LogError("[Multiplayer] 보이스 연결 실패: 세션 ID를 가져올 수 없습니다.");
        }
    }

    #endregion

    #region Authentication
    public async Task EnsureSignedInAsync()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[Multiplayer] 로그인 성공: {AuthenticationService.Instance.PlayerId}");
        }
    }
    #endregion

    #region Session Management (Create / Join / Query / Cancel)

    // 1. 방 만들기 (Create)
    public async void CreateSessionAsync(string sessionName)
    {
        //_isLeaving = false;
        //try
        //{
        //    await EnsureSignedInAsync();

        //    // 1. NetworkManager 존재 여부 최우선 확인
        //    if (NetworkManager.Singleton == null)
        //    {
        //        Debug.LogError("[Multiplayer] NetworkManager가 씬에 존재하지 않습니다!");
        //        return;
        //    }

        //    var options = new SessionOptions
        //    {
        //        Name = sessionName,
        //        MaxPlayers = 4,
        //        IsPrivate = false
        //    }.WithRelayNetwork();

        //    ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);

        //    // 2. Relay 할당
        //    var allocation = await RelayService.Instance.CreateAllocationAsync(ActiveSession.MaxPlayers);

        //    // 3. UnityTransport 참조 확인
        //    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        //    if (transport == null)
        //    {
        //        Debug.LogError("[Multiplayer] NetworkManager에 UnityTransport 컴포넌트가 없습니다!");
        //        return;
        //    }

        //    // 4. Relay 데이터 설정
        //    transport.SetHostRelayData(
        //        allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
        //        allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData
        //    );

        //    // 5. 호스트 시작
        //    NetworkManager.Singleton.StartHost();
        //    OnHostStatusChanged?.Invoke(true);

        //    Debug.Log($"[Multiplayer] 세션 생성 성공: {ActiveSession.Name}");

        //    // GameSceneManager가 Null인지도 체크
        //    if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadNetworkScene(LOBBY_SCENE_NAME);
        //    else
        //        Debug.LogError("[Multiplayer] GameSceneManager 인스턴스를 찾을 수 없습니다.");
        //}
        //catch (Exception e)
        //{
        //    Debug.LogError($"[Multiplayer] 세션 생성 실패: {e.Message}");
        //}

        // 이전 작업이 있다면 취소 후 새로 생성
        CancelSessionOperations();
        _sessionCancelSource = new CancellationTokenSource();
        var token = _sessionCancelSource.Token;

        try
        {
            await EnsureSignedInAsync();

            // 여기서 Relay 자동 할당
            var options = new SessionOptions
            {
                Name = sessionName,
                MaxPlayers = 4,
                IsPrivate = false
            }.WithRelayNetwork(); 

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            // 만약 대기 중에 취소되었다면 중단
            if (token.IsCancellationRequested)
            {
                Debug.Log("[Multiplayer] 방 생성 직후 취소 요청 감지. 서버에서 방을 즉시 제거합니다.");
                await ActiveSession.LeaveAsync();
                ActiveSession = null;
                return;
            }

            CurrentChannelId = ActiveSession.Id;

            // 중요: 별도의 Relay 할당 코드를 작성하지 마세요. 
            // ActiveSession.Code에 이미 Relay 코드가 담겨 있습니다.
            string joinCode = ActiveSession.Code;

            if (string.IsNullOrEmpty(joinCode))
            {
                //Debug.LogError("Join Code 생성 실패");
                return;
            }

            NetworkManager.Singleton.StartHost();
            Debug.Log($"[Multiplayer] 호스트 시작 성공! 코드: {joinCode}");

            if (gameSessionManagerPrefab != null)
            {
                GameSessionManager.SpawnManager(gameSessionManagerPrefab);
            }

            if (gameManager_ServerPrefab != null)
            {
                GameMaster.SpawnManager(gameManager_ServerPrefab);
            }

            if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadNetworkScene(LOBBY_SCENE_NAME);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("방 생성 작업이 사용자에 의해 취소되었습니다.");
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested) Debug.LogError($"[Multiplayer] 세션 생성 실패: {e.Message}");
        }
    }

    // 2. 방 목록 불러오기 (Query) - Join 버튼 클릭 시 호출용
    public async void QuerySessionsAsync()
    {
        if (_isQuerying) return; // 이미 실행 중이면 무시
        _isQuerying = true;

        try
        {
            await EnsureSignedInAsync();

            // 에러 해결: Options에 Limit이 없다면 기본 생성자 사용 후 속성 설정
            var queryOptions = new QuerySessionsOptions();
            // 만약 queryOptions.Count 나 queryOptions.MaxResults 등도 안된다면 일단 비워둡니다.

            // 쿼리 실행
            var queryResponse = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);

            // 에러 해결: ISession 대신 ISessionInfo 리스트 생성
            List<ISessionInfo> sessions = new List<ISessionInfo>();

            if (queryResponse != null && queryResponse.Sessions != null)
            {
                foreach (var session in queryResponse.Sessions)
                {
                    // 이제 ISessionInfo 형식으로 리스트에 담깁니다.
                    sessions.Add(session);
                }
            }

            Debug.Log($"[Multiplayer] {sessions.Count}개의 방을 찾았습니다.");
            OnSessionListUpdated?.Invoke(sessions);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Multiplayer] 방 목록 불러오기 실패: {e.Message}");
        }
        finally { _isQuerying = false; }
    }
    // 3. 특정 방에 참가하기 (Join)
    public async void JoinSessionAsync(ISessionInfo session)
    {
        CancelSessionOperations();
        _sessionCancelSource = new CancellationTokenSource();
        CurrentChannelId = session.Id;

        try
        {
            await EnsureSignedInAsync();
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(session.Id);

            // [중요] 실제 세션 객체에서 받은 ID로 재확정
            if (ActiveSession != null) CurrentChannelId = ActiveSession.Id;

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.StartClient();
            }

            // 새 씬에서 매니저들이 새 UI/Recorder를 찾도록 명령
            ResetSessionState();
        }
        catch (Exception e)
        {
            CurrentChannelId = "LobbyChannel";
            Debug.LogError($"[Multiplayer] 세션 참가 실패: {e.Message}");
        }
    }

    public async void CancelSessionOperations()
    {
        CurrentChannelId = "LobbyChannel";

        if (_sessionCancelSource != null)
        {
            _sessionCancelSource.Cancel();
            _sessionCancelSource.Dispose();
            _sessionCancelSource = null;
            Debug.Log("[Multiplayer] 진행 중인 모든 작업을 취소했습니다.");
        }

        // 2. 만약 이미 세션이 생성되어 있다면 서버에서 제거
        if (ActiveSession != null)
        {
            try
            {
                // 호스트인 경우 세션을 떠나면 일반적으로 세션이 삭제되거나 유효하지 않게 됩니다.
                await ActiveSession.LeaveAsync();
            }
            catch (System.Exception e)
            {
                // 'lobby not found'나 'session not started'는 취소 상황에서 빈번하므로 
                // 에러가 아닌 정보성 로그로 처리하거나 무시합니다.
                if (e.Message.Contains("lobby not found") || e.Message.Contains("never started"))
                {
                    Debug.Log("[Multiplayer] 세션이 아직 생성 전이거나 이미 정리되었습니다.");
                }
                else
                {
                    Debug.LogWarning($"[Multiplayer] 세션 정리 중 예외 발생: {e.Message}");
                }
            }
            finally
            {
                ActiveSession = null;
            }
        }


        // UI에서 취소를 눌렀을 때 NetworkManager가 이미 시작되었다면 셧다운
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
    #endregion

    #region 방 잠그기
    // 💡 1. 세션 업데이트 불량 문제를 LobbyService 직접 호출로 해결
    public async Task LockSessionAsync()
    {
        if (ActiveSession == null || !NetworkManager.Singleton.IsServer) return;

        try
        {
            // 래퍼인 Session 대신 근간이 되는 Lobby 자체를 잠가버립니다.
            // ActiveSession.Id는 내부적으로 Lobby ID와 동일합니다.
            var updateOptions = new UpdateLobbyOptions { IsLocked = true };
            await LobbyService.Instance.UpdateLobbyAsync(ActiveSession.Id, updateOptions);

            Debug.Log("<color=green>[Session]</color> 방 잠금 완료. 방 목록에 '게임중'으로 표시됩니다.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Session] 방 잠금 시각적 갱신 실패: {e.Message}");
        }
    }

    //// 💡 2. 방장 이탈 감지 (방 폭파)
    //private void OnClientDisconnected(ulong clientId)
    //{
    //    // 내가 클라이언트인데 (방장이 아님)
    //    if (!NetworkManager.Singleton.IsServer)
    //    {
    //        // 방장(ServerClientId)의 연결이 끊어졌거나, 내 연결이 끊겼을 때
    //        if (clientId == NetworkManager.ServerClientId || clientId == NetworkManager.Singleton.LocalClientId)
    //        {
    //            Debug.Log("<color=red>[Multiplayer] 서버(방장)와의 연결이 종료되어 타이틀로 귀환합니다.</color>");
    //            ReturnToLobbyLocal();
    //        }
    //    }
    //}

    // 💡 3. 클라이언트 강제 귀환 로직
    public async void ReturnToLobbyLocal()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();

        // 2. 세션 리스트에서 이탈
        if (ActiveSession != null)
        {
            try { await ActiveSession.LeaveAsync(); }
            catch { }
            finally { ActiveSession = null; }
        }

        // 클라이언트가 들고 있던 채널ID 초기화
        CurrentChannelId = "LobbyChannel";

        _isLeaving = false;

        // 중요: 타이틀로 돌아가기 전, 세션 리스트를 비우도록 이벤트 발행
        OnSessionListUpdated?.Invoke(new List<ISessionInfo>());

        UnityEngine.SceneManagement.SceneManager.LoadScene(START_SCENE_NAME);
    }
    #endregion

    #region Teardown & Callbacks
    public async Task RequestDeleteSession()
    {
        if (ActiveSession == null) return;

        string sessionIdToKill = ActiveSession.Id;

        // 1. 호스트인 경우, 방을 업데이트하는 대신 즉시 삭제(Delete)해버립니다.
        if (NetworkManager.Singleton.IsServer)
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(sessionIdToKill);
                Debug.Log("<color=yellow>[Session]</color> 로비 서비스에서 방 삭제 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"방 삭제 실패 (이미 지워졌을 수 있음): {e.Message}");
            }
        }

        LeaveSession();
    }

    public async void LeaveSession() // 기존 호출용
    {
        if (_isLeaving || NetworkManager.Singleton == null) return;
        _isLeaving = true;

        // 1. 🧹 모든 보이스/채팅 중지
        if (GlobalVoiceManager.Instance != null) GlobalVoiceManager.Instance.ShutdownVoice();

        PhotonChatManager chatManager = FindFirstObjectByType<PhotonChatManager>();
        if (chatManager != null) chatManager.DisconnectAndClear();

        // 2. 🧹 폰 상태 초기화 (입력 막힘 방지)
        if (PhoneUIController.Instance != null) PhoneUIController.Instance.ResetPhoneState();

        // 3. 🧹 서버 아이템 정리 (서버일 때만)
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
        {
            GameSessionManager.Instance?.CleanupMonstersInScene();
            GameSessionManager.Instance?.CleanupAllItemsInScene();
            await Task.Delay(200);
        }

        // 4. 🧹 세션 서비스 이탈 및 넷코드 종료
        try
        {
            if (ActiveSession != null) await ActiveSession.LeaveAsync();
        }
        catch { }
        finally
        {
            // 💡 Shutdown은 무조건 실행되도록 finally에 배치
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();

            ActiveSession = null;
            PlayerController.AllPlayers.Clear();
            CurrentChannelId = "LobbyChannel";
            _isLeaving = false;

            // 마지막에 씬 이동
            SceneManager.LoadScene(START_SCENE_NAME);
        }
    }
    private void OnClientDisconnected(ulong clientId)
    {
        // 내가 클라이언트인데 (방장이 아님)
        if (!NetworkManager.Singleton.IsServer)
        {
            //// 방장(ServerClientId)의 연결이 끊어졌거나, 내 연결이 끊겼을 때
            //if (clientId == NetworkManager.ServerClientId || clientId == NetworkManager.Singleton.LocalClientId)
            //{
            //    // 타이틀로 돌아가기 전, 내가 접속해있던 방이 폭파된 것이므로 ID를 기록해둡니다.
            //    if (ActiveSession != null)
            //    {
            //        ExplodedSessionId = ActiveSession.Id;
            //    }

            //    Debug.Log("<color=red>[Multiplayer] 서버(방장)와의 연결이 종료되어 타이틀로 귀환합니다.</color>");
            //    ReturnToLobbyLocal();
            //
            //}
            if (clientId == NetworkManager.ServerClientId || clientId == NetworkManager.Singleton.LocalClientId)
            {
                // 중복 실행 방지
                if (_isLeaving) return;

                Debug.Log("<color=red>[Multiplayer] 서버와의 연결 종료. 타이틀로 귀환합니다.</color>");

                // 💡 복잡한 비동기 로직 대신 통합된 LeaveSession 하나만 호출합니다.
                LeaveSession();
            }
        }
    }

    //public async void LeaveSession()
    //{
    //    if (_isLeaving) return;
    //    _isLeaving = true;

    //    Debug.Log("<color=orange>[Multiplayer]</color> 세션 종료 및 클린업 시작...");

    //    // 1. 포톤 보이스 먼저 안전하게 끊기
    //    if (GlobalVoiceManager.Instance != null && GlobalVoiceManager.Instance.globalVoiceClient != null)
    //    {
    //        try { GlobalVoiceManager.Instance.globalVoiceClient.Client.Disconnect(); }
    //        catch { }
    //    }

    //    // 2. 서버(호스트)인 경우 몬스터/아이템 정리
    //    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
    //    {
    //        if (GameSessionManager.Instance != null)
    //        {
    //            GameSessionManager.Instance.CleanupMonstersInScene();
    //            GameSessionManager.Instance.CleanupAllItemsInScene();
    //        }
    //        await Task.Delay(300); // 패킷 전송 대기
    //    }

    //    // 3. [핵심 수정] 세션 이탈 (에러 무시 처리)
    //    if (ActiveSession != null)
    //    {
    //        try
    //        {
    //            // 💡 세션이 이미 파괴되었을 가능성이 높으므로 타임아웃을 짧게 잡거나 예외를 무시합니다.
    //            await ActiveSession.LeaveAsync();
    //            Debug.Log("[Multiplayer] 세션 서비스 이탈 완료");
    //        }
    //        catch (Exception e)
    //        {
    //            // 이미 로비가 없거나 멤버가 아니라는 에러는 무시하고 넘어갑니다.
    //            Debug.Log($"[Multiplayer] 세션 서비스 이탈 중 예상된 예외(무시가능): {e.Message}");
    //        }
    //        finally { ActiveSession = null; }
    //    }

    //    // 4. 네트워크 매니저 셧다운
    //    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
    //    {
    //        NetworkManager.Singleton.Shutdown();
    //        Debug.Log("[Multiplayer] NetworkManager 셧다운 완료");
    //    }

    //    // 5. 상태 및 정적 데이터 초기화
    //    PlayerController.AllPlayers.Clear();
    //    CurrentChannelId = "LobbyChannel";
    //    ExplodedSessionId = "";

    //    _isLeaving = false;

    //    // 5. 씬 이동
    //    SceneManager.LoadScene(START_SCENE_NAME);
    //}

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    #endregion

    #region 씬 이동 시 참조 파괴 방지 스크립트
    public void ResetSessionState()
    {
        // 1. UI 갱신
        //if (PlayerUIManager.LocalInstance != null) PlayerUIManager.LocalInstance.RefreshUIReferences();

        //if (GlobalVoiceManager.Instance != null)
        //{
        //    StopAllCoroutines();
        //    StartCoroutine(DelayedVoiceReset());
        //}

        StartCoroutine(DelayedUIRefresh());

        if (GlobalVoiceManager.Instance != null)
        {
            StopAllCoroutines();
            StartCoroutine(DelayedVoiceReset());
        }
    }
    IEnumerator DelayedUIRefresh()
    {
        yield return null;
        if (PlayerUIManager.LocalInstance != null)
        {
            PlayerUIManager.LocalInstance.RefreshUIReferences();
        }
    }

    IEnumerator DelayedVoiceReset()
    {
        // 너무 짧으면 씬 로딩 중이라 객체를 못 찾고, 너무 길면 유저가 답답해합니다.
        yield return new WaitForSeconds(1.0f);

        // 💡 [수정] 씬에 있는 '내' Recorder와 VoiceClient를 찾아서 갱신합니다.
        // 기존에 붙어있던 DontDestroy 객체의 컴포넌트를 유지하는 게 아니라,
        // 새 씬에서 갱신된 하드웨어 참조를 덮어씌웁니다.
        var recorder = FindFirstObjectByType<Photon.Voice.Unity.Recorder>();
        var voiceClient = FindFirstObjectByType<UnityVoiceClient>();

        if (GlobalVoiceManager.Instance != null)
        {
            if (recorder != null) GlobalVoiceManager.Instance.globalRecorder = recorder;
            if (voiceClient != null) GlobalVoiceManager.Instance.globalVoiceClient = voiceClient;

            string nick = PlayerNickname; // 또는 저장된 닉네임
            GlobalVoiceManager.Instance.ConnectVoice(nick, CurrentChannelId);
        }

        //if (newRecorder != null)
        //{
        //    GlobalVoiceManager.Instance.globalRecorder = newRecorder;
        //    GlobalVoiceManager.Instance.globalVoiceClient.PrimaryRecorder = newRecorder;
        //    Debug.Log("<color=cyan>[System]</color> 리코더 참조 갱신 완료");
        //}

        //string nick = GlobalVoiceManager.Instance.globalVoiceClient.Client.NickName;
        //if (string.IsNullOrEmpty(nick)) nick = PlayerNickname;

        //Debug.Log($"<color=yellow>[System]</color> 보이스 엔진 재접속 프로세스 시작: {CurrentChannelId}");
        //GlobalVoiceManager.Instance.ConnectVoice(nick, CurrentChannelId);
    }

    #endregion

    void OnApplicationQuit()
    {
        _isLeaving = true;
        CancelSessionOperations();
    }
}