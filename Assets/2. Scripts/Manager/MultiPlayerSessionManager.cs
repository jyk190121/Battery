using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class MultiPlayerSessionManager : NetworkBehaviour
{
    public static MultiPlayerSessionManager Instance { get; private set; }

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

    // 로컬 플레이어 닉네임 (UI에서 입력받아 설정)
    private string _playerNickname = "Guest"; // 기본값 설정

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
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleNetcodeConnected;
        }

        if (IsClient && IsOwner)
        {
            HandleNetcodeConnected(NetworkManager.Singleton.LocalClientId);
        }
    }

    private void HandleNetcodeConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            StartCoroutine(InitializePhotonServicesRoutine());
        }
    }

    IEnumerator InitializePhotonServicesRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        if (GlobalVoiceManager.Instance != null)
        {
            GlobalVoiceManager.Instance.InitVoice(PlayerNickname);
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
    }
    // 3. 특정 방에 참가하기 (Join)
    public async void JoinSessionAsync(ISessionInfo session)
    {
        //_isLeaving = false;
        //try
        //{
        //    await EnsureSignedInAsync();

        //    // 에러 해결: ISessionInfo에서 세션 ID를 가져와 참가
        //    ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(session.Id);

        //    // Relay 접속 정보 추출 (ActiveSession.Code 또는 Relay 할당 정보 사용)
        //    var joinAllocation = await RelayService.Instance.JoinAllocationAsync(ActiveSession.Code);
        //    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        //    if (transport != null)
        //    {
        //        transport.SetClientRelayData(
        //            joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port,
        //            joinAllocation.AllocationIdBytes, joinAllocation.Key,
        //            joinAllocation.ConnectionData, joinAllocation.HostConnectionData
        //        );
        //    }

        //    // 클라이언트 시작
        //    NetworkManager.Singleton.StartClient();
        //    OnHostStatusChanged?.Invoke(false);

        //    Debug.Log($"[Multiplayer] 세션 참가 성공: {ActiveSession.Name}");

        //    if (GameSceneManager.Instance != null)
        //        GameSceneManager.Instance.LoadNetworkScene(LOBBY_SCENE_NAME);
        //}
        //catch (Exception e)
        //{
        //    Debug.LogError($"[Multiplayer] 세션 참가 실패: {e.Message}");
        //}

        //try
        //{
        //    await EnsureSignedInAsync();

        //    // 1. 세션 서비스 참가
        //    ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(session.Id);

        //    // 2. [수정] 세션으로부터 Relay Join Code 추출
        //    // 만약 ActiveSession.Code가 null이라면 세션 옵션 설정을 다시 확인해야 합니다.
        //    string relayJoinCode = ActiveSession.Code;

        //    if (string.IsNullOrEmpty(relayJoinCode))
        //    {
        //        Debug.LogError("Join Code를 찾을 수 없습니다. 호스트의 세션 설정을 확인하세요.");
        //        return;
        //    }

        //    // 3. Relay 접속
        //    var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
        //    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        //    transport.SetClientRelayData(
        //        joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port,
        //        joinAllocation.AllocationIdBytes, joinAllocation.Key,
        //        joinAllocation.ConnectionData, joinAllocation.HostConnectionData
        //    );

        //    NetworkManager.Singleton.StartClient();
        //    Debug.Log($"[Multiplayer] Relay 코드({relayJoinCode})로 참가 성공");
        //}
        //catch (Exception e)
        //{
        //    Debug.LogError($"[Multiplayer] 세션 참가 실패: {e.Message}");
        //}

        CancelSessionOperations();
        _sessionCancelSource = new CancellationTokenSource();
        var token = _sessionCancelSource.Token;

        CurrentChannelId = session.Id;

        try
        {
            await EnsureSignedInAsync();

            // 1. 세션 서비스 참가 (이 내부에서 Relay 연결이 자동으로 준비됩니다)
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(session.Id);
            
            // 이미 세션에 들어갔다면 바로 나가기 처리
            if (token.IsCancellationRequested)
            {
                await ActiveSession.LeaveAsync();
                return;
            }

            // [중요] 여기서 RelayService.Instance.JoinAllocationAsync를 직접 호출하지 마세요!
            // WithRelayNetwork()를 사용하면 세션에 참가하는 순간 
            // 하단의 NetworkManager와 Transport 설정이 자동으로 연동되거나 
            // 내부 엔진이 처리하도록 설계되어 있습니다.

            // 2. NetworkManager 시작 (클라이언트)
            // Transport 데이터 설정은 세션 라이브러리가 내부적으로 처리하므로 바로 StartClient를 시도합니다.
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.StartClient();
            }

            Debug.Log($"[Multiplayer] 세션 참가 완료: {ActiveSession.Name}");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("방 참가 작업이 사용자에 의해 취소되었습니다.");
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested) Debug.LogError($"[Multiplayer] 세션 참가 실패: {e.Message}");
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

    #region Teardown & Callbacks

    public async void LeaveSession()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (ActiveSession != null)
        {
            try
            {
                await ActiveSession.LeaveAsync();
            }
            catch (Exception e) { Debug.LogWarning(e.Message); }
            finally { ActiveSession = null; }
        }

        _isLeaving = false;
        GameSceneManager.Instance.LoadNetworkScene(START_SCENE_NAME);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // 호스트가 나갔거나 내가 튕겼을 때
        if (!NetworkManager.Singleton.IsServer)
        {
            if (clientId == NetworkManager.ServerClientId || clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("<color=red>서버와의 연결이 종료되었습니다.</color>");
                LeaveSession();
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    #endregion
}