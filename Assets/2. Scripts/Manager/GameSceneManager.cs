using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : NetworkBehaviour
{
    public static GameSceneManager Instance { get; private set; }
    public float LoadingProgress { get; private set; }          // 로딩 진행률을 외부(UI)에서 읽을 수 있도록 공개

    [Header("설정")]
    public string playerPrefabName = "Player_KJY";              // 사용할 플레이어 프리팹 이름
    public string spawnPointName = "PlayerSpawnZone";           // Lobby Scene

    Transform[] spawnPoints;                                    // 씬에서 찾은 스폰포인트들 저장

    // 게임 세션 시작을 알리는 이벤트
    public event System.Action OnGameSessionRequest;

    public bool IsSessionInitialized { get; private set; }

    private void Awake()
    {
        // 싱글톤 핵심 로직 수정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);                      // 이 객체를 씬이 바뀌어도 보존
        }
        else
        {
            Destroy(gameObject);                                // 중복 생성된 객체는 제거
        }
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

        }
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLocalLoadComplete;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnLocalLoadComplete;
        }
    }

    /// <summary>
    /// 멀티플레이어 안전 씬 로드 (서버에서만 호출 가능)
    /// </summary>
    public void LoadNetworkScene(string sceneName)
    {
        if (IsServer && NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
    ///// <summary>
    ///// 플레이어 리스폰 포지션 초기화
    ///// </summary>
    ///// <param name="clientId"></param>
    ///// <returns></returns>

    //public (Vector3 position, Quaternion rotation) GetSpawnPoint(ulong clientId)
    //{
    //    // 1. 최신 스폰 포인트 정보 갱신
    //    UpdateSpawnPoints();

    //    if (spawnPoints != null && spawnPoints.Length > 0)
    //    {
    //        // 2. ClientId를 기반으로 배정된 인덱스 계산
    //        int spawnIndex = GetSpawnIndex(clientId);
    //        Transform targetPoint = spawnPoints[spawnIndex % spawnPoints.Length];

    //        return (targetPoint.position, targetPoint.rotation);
    //    }

    //    // 스폰 포인트가 없을 경우 기본값 반환
    //    Debug.LogWarning("[GameSceneManager] 스폰 포인트를 찾지 못해 기본 위치를 반환합니다.");
    //    return (Vector3.zero, Quaternion.identity);
    //}

    ///// <summary>
    ///// 모든 클라이언트가 씬 로딩을 마쳤을 때 호출되는 콜백 (서버에서 실행)
    ///// </summary>
    //private void OnAllClientsLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    //{
    //    // 1. 인게임 씬인지 확인 (타이틀/로비 씬 제외)

    //    if (sceneName == "KJY_Lobby") // 이동할 로비 씬 이름
    //    {
    //        foreach (ulong clientId in clientsCompleted)
    //        {
    //            SpawnPlayerAtPosition(clientId);
    //        }
    //    }
    //}

    private void OnClientConnected(ulong clientId)
    {
        // 현재 로비 씬인 경우에만 즉시 스폰을 시도합니다.
        if (SceneManager.GetActiveScene().name == "KJY_Lobby" || SceneManager.GetActiveScene().name == "KJY_Player")
        {
            Debug.Log($"[GameSceneManager] 클라이언트 {clientId} 접속 감지. 스폰 시도");
            SpawnPlayerAtPosition(clientId);
        }
    }
    void OnSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "KJY_Lobby" || sceneName == "KJY_Player")
        {
            foreach (var clientId in clientsCompleted)
            {
                // 해당 클라이언트의 플레이어 객체를 찾아 위치를 잡아줍니다.
                SpawnPlayerAtPosition(clientId);
            }

            // 게임 세션 초기화가 필요한 경우 호출
            RequestStartGameServerRpc();
        }
    }

    //private void SpawnPlayerForClient(ulong clientId)
    //{
    //    // 이미 플레이어 객체가 있다면 생성하지 않음
    //    if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null) return;

    //    // 플레이어 프리팹은 NetworkManager에 등록된 Default Player Prefab이 사용됩니다.
    //    // 특정 좌표에 소환하고 싶다면 아래 주석을 활용하세요.
    //     Vector3 spawnPos = new Vector3(40.14f, 0.66f, 67.41f);
    //    NetworkManager.Singleton.GetNetworkPrefabOverride(NetworkManager.Singleton.NetworkConfig.PlayerPrefab);

    //    // 주의: NetworkManager 설정에서 'Auto Object Parent Sync'와 'Spawn Player' 옵션에 따라 
    //    // 자동으로 생성될 수도 있습니다. 수동 제어를 원하면 NetworkManager 설정을 확인하세요.
    //}

    void SpawnPlayerAtPosition(ulong clientId)
    {
        if (!IsServer) return;

        // 1. 현재 씬에서 "PlayerSpawnZone"을 찾아 spawnPoints 배열 갱신
        UpdateSpawnPoints();

        // 2. 위치 데이터 계산
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = GetSpawnIndex(clientId);
            Transform targetPoint = spawnPoints[spawnIndex % spawnPoints.Length];
            spawnPos = targetPoint.position;
            spawnRot = targetPoint.rotation;
            Debug.Log($"[GameSceneManager] 클라이언트 {clientId}를 {targetPoint.name} 위치({spawnPos})로 배정합니다.");
        }
        else
        {
            spawnPos = Vector3.zero;

            Debug.LogWarning("[GameSceneManager] 스폰 포인트가 없어 zero 좌표를 사용합니다.");
        }

        FinalizeSpawn(clientId, spawnPos, spawnRot);
    }
    void FinalizeSpawn(ulong clientId, Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            // A. 이미 플레이어 객체가 있는 경우 (씬 이동 시 위치 재설정)
            if (client.PlayerObject != null)
            {
                var pc = client.PlayerObject.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.RevivePlayer();

                    // [중요] 서버에서 직접 바꾸지 않고, 권한을 가진 Owner 클라이언트에게 이동을 명령합니다.
                    pc.TeleportToSpawnClientRpc(pos, rot);
                }
            }
            else
            {
                var networkPrefab = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs
                    .FirstOrDefault(p => p.Prefab.name == playerPrefabName);

                if (networkPrefab != null && networkPrefab.Prefab != null)
                {
                    GameObject playerObj = Instantiate(networkPrefab.Prefab, pos, rot);
                    var networkObj = playerObj.GetComponent<NetworkObject>();
                    networkObj.SpawnAsPlayerObject(clientId);
                }
            }
        }

        if (IsOwner)
        {
            // 1. 캐릭터 컨트롤러가 있다면 잠시 끄기 (충돌로 인한 튕김 방지)
            var canvas = GetComponent<CharacterController>();
            if (canvas != null) canvas.enabled = false;

            // 2. 위치 이동
            transform.SetPositionAndRotation(pos, rot);

            // 3. NetworkTransform이 있다면 상태 동기화 강제 업데이트
            if (TryGetComponent(out Unity.Netcode.Components.NetworkTransform nt))
            {
                // Teleport 메서드는 현재 위치를 즉시 동기화 포인트로 잡습니다.
                nt.Teleport(pos, rot, transform.localScale);
            }

            if (canvas != null) canvas.enabled = true;

            Debug.Log($"[Player] 스폰 위치로 텔레포트 완료: {pos}");
        }
    }

    // 로컬 클라이언트에서 씬 로드가 끝났을 때 실행됨
    private void OnLocalLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        // 내 로컬 캐릭터를 찾아서 UI를 다시 연결해줌
        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            //var interaction = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerInteraction>();
            //if (interaction != null)
            //{
            //    interaction.FindUIElements();
            //}

            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;

            // 상호작용 UI 연결
            if (playerObj.TryGetComponent(out PlayerInteraction interaction)) interaction.FindUIElements();
        }
    }

    void UpdateSpawnPoints()
    {
        GameObject playerSpawnArray = GameObject.Find(spawnPointName);

        if (playerSpawnArray != null)
        {
            spawnPoints = playerSpawnArray.GetComponentsInChildren<Transform>()
                .Where(t => t != playerSpawnArray.transform) // 부모 자신은 제외
                .OrderBy(t => $"Spawn{t.name}")              // 이름순 정렬
                .ToArray();

            Debug.Log($"[GameSceneManager] {SceneManager.GetActiveScene().name}에서 {spawnPoints.Length}개 포인트 발견.");
        }
        else
        {
            spawnPoints = null;
        }
    }
    int GetSpawnIndex(ulong clientId)
    {
        var connectedList = NetworkManager.Singleton.ConnectedClientsList;

        for (int i = 0; i < connectedList.Count; i++)
        {
            if (connectedList[i].ClientId == clientId)
            {
                return i;
            }
        }

        return 0; // 찾지 못했을 경우 기본값
    }


    //[ServerRpc(RequireOwnership = false)]
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestStartGameServerRpc()
    {
        if (!IsServer) return;

        IsSessionInitialized = true;
        OnGameSessionRequest?.Invoke();
    }

    public void ResetSessionState()
    {
        IsSessionInitialized = false;
    }

    // --- 로컬 전용 (필요 시 활용) ---

    public void RestartScene()
    {
        if (IsServer) LoadNetworkScene(SceneManager.GetActiveScene().name);
    }


    // 기본 로드 방식 (동기)
    public void LoadScene(string sceneName)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }

    //// 비동기 로드 방식 (추천: 로딩 화면 구현 시 유리)
    //public void LoadSceneAsync(string sceneName)
    //{
    //    //if (sceneName == "BattleScene")
    //    //{
    //    //    InitializeBattle();
    //    //}
    //    StartCoroutine(LoadSceneCoroutine(sceneName));
    //}

    //private IEnumerator LoadSceneCoroutine(string sceneName)
    //{
    //    // 1. 먼저 로딩 화면(정거장)으로 이동합니다.
    //    // 로딩 씬은 가벼우므로 동기 로딩해도 무방합니다.
    //    SceneManager.LoadScene("LoadingScene");
    //    yield return null;                      // 한 프레임 대기 (로딩 씬의 UI가 뜰 시간을 줌)

    //    // 2. 이제 실제 목표 씬을 비동기로 로드합니다.
    //    AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
    //    op.allowSceneActivation = false; // 90%에서 멈춰두기

    //    float startValue = LoadingProgress; // 현재 값 저장
    //    float fakeProgress = 0f;            // 연출용 변수

    //    //float timer = 0f;
    //    while (!op.isDone)
    //    {
    //        //// 진행률 계산 (0~1)
    //        //LoadingProgress = Mathf.Clamp01(op.progress / 0.9f);
    //        //print($"현재 씬 로드 진행률 : {LoadingProgress}");

    //        //// 로딩 완료 조건
    //        //if (op.progress >= 0.9f)
    //        //{
    //        //    yield return new WaitForSeconds(0.1f);

    //        //    // 연출을 위해 약간의 지연을 주거나 바로 전환
    //        //    op.allowSceneActivation = true;
    //        //}

    //        //yield return null;

    //        yield return null;

    //        // 1. 유니티의 실제 로딩 수치 (0.9가 최대)
    //        float realTarget = op.progress / 0.9f;

    //        // 2. 가짜 진행률을 실제 진행률을 따라가게 함 (부드럽게)
    //        // MoveTowards는 일정한 속도로 증가시켜서 Lerp보다 제어가 쉽습니다.
    //        fakeProgress = Mathf.MoveTowards(fakeProgress, realTarget, Time.unscaledDeltaTime * 0.5f);

    //        LoadingProgress = fakeProgress;

    //        // 3. 90% 완료 시 100%까지 강제 연출
    //        if (op.progress >= 0.9f)
    //        {
    //            fakeProgress = Mathf.MoveTowards(fakeProgress, 1f, Time.unscaledDeltaTime * 0.5f);
    //            LoadingProgress = fakeProgress;

    //            if (LoadingProgress >= 1f)
    //            {
    //                op.allowSceneActivation = true;
    //            }
    //        }

    //        //Debug.Log($"보정된 로딩 진행률 : {LoadingProgress * 100}%");
    //    }
    //}

    //// 현재 씬 재시작
    //public void RestartScene()
    //{
    //    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    //}

    public string SceneName()
    {
        return SceneManager.GetActiveScene().name;
    }
}