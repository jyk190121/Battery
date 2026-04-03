using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using UnityEngine;

public class MultiPlayerSessionManager : NetworkBehaviour
{
    public static MultiPlayerSessionManager Instance { get; private set; }

    [Header("설정")]
    private const string LOBBY_SCENE_NAME = "KJY_Lobby";
    private const string START_SCENE_NAME = "KJY_Player";

    // 현재 활성화된 세션 정보
    public ISession ActiveSession { get; private set; }

    // 로컬 플레이어 닉네임 (UI에서 입력받아 설정)
    private string _playerNickname = "Guest"; // 기본값 설정

    public string PlayerNickname
    {
        get => _playerNickname;
        set
        {
            // 공백 체크 등 최소한의 검증 후 저장
            if (!string.IsNullOrWhiteSpace(value))
                _playerNickname = value;
        }
    }

    public void SetNickname(string name)
    {
        PlayerNickname = name;
        PlayerPrefs.SetString("SavedNickname", name); // 로컬 기기에 저장
    }

    // UI 갱신을 위한 이벤트 (방 목록 전달)
    public event Action<List<ISession>> OnSessionListUpdated;
    public event Action<bool> OnHostStatusChanged;

    private bool _isLeaving = false;

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

    #region Session Management (Create / Join / Query)

    // 1. 방 만들기 (Create)
    public async void CreateSessionAsync(string sessionName)
    {
        _isLeaving = false;
        try
        {
            await EnsureSignedInAsync();

            // 1. NetworkManager 존재 여부 최우선 확인
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[Multiplayer] NetworkManager가 씬에 존재하지 않습니다!");
                return;
            }

            var options = new SessionOptions
            {
                Name = sessionName,
                MaxPlayers = 4,
                IsPrivate = false
            }.WithRelayNetwork();

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            // 2. Relay 할당
            var allocation = await RelayService.Instance.CreateAllocationAsync(ActiveSession.MaxPlayers);

            // 3. UnityTransport 참조 확인
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[Multiplayer] NetworkManager에 UnityTransport 컴포넌트가 없습니다!");
                return;
            }

            // 4. Relay 데이터 설정
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData
            );

            // 5. 호스트 시작
            NetworkManager.Singleton.StartHost();
            OnHostStatusChanged?.Invoke(true);

            Debug.Log($"[Multiplayer] 세션 생성 성공: {ActiveSession.Name}");

            // GameSceneManager가 Null인지도 체크
            if (GameSceneManager.Instance != null)
                GameSceneManager.Instance.LoadScene(LOBBY_SCENE_NAME);
            else
                Debug.LogError("[Multiplayer] GameSceneManager 인스턴스를 찾을 수 없습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Multiplayer] 세션 생성 실패: {e.Message}");
        }
    }

    // 2. 방 목록 불러오기 (Query) - Join 버튼 클릭 시 호출용
    public async void QuerySessionsAsync()
    {
        try
        {
            await EnsureSignedInAsync();

            //var queryOptions = new QuerySessionsOptions { MaxResults = 10 };
            //var queryResponse = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);

            //Debug.Log($"[Multiplayer] {queryResponse.Results.Count}개의 방을 찾았습니다.");
            //OnSessionListUpdated?.Invoke(queryResponse.Results);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Multiplayer] 방 목록 불러오기 실패: {e.Message}");
        }
    }

    // 3. 특정 방에 참가하기 (Join)
    public async void JoinSessionAsync(ISession session)
    {
        _isLeaving = false;
        try
        {
            await EnsureSignedInAsync();

            // 세션 참가
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(session.Id);

            // Relay 접속 정보 추출
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(ActiveSession.Code);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes, joinAllocation.Key,
                joinAllocation.ConnectionData, joinAllocation.HostConnectionData
            );

            // 클라이언트 시작
            NetworkManager.Singleton.StartClient();
            OnHostStatusChanged?.Invoke(false);

            Debug.Log($"[Multiplayer] 세션 참가 성공: {ActiveSession.Name}");

            GameSceneManager.Instance.LoadScene(LOBBY_SCENE_NAME);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Multiplayer] 세션 참가 실패: {e.Message}");
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
        UnityEngine.SceneManagement.SceneManager.LoadScene(START_SCENE_NAME);
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