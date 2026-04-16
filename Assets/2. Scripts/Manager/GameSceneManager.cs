using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
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
    public event Action OnGameSessionRequest;

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
        if (sceneName == "KJY_Lobby")
        {
            Debug.Log("[GameSceneManager] 로비 씬 로드 완료. 현재 접속된 모든 인원 스폰 시도");
            foreach (var clientId in clientsCompleted)
            {
                SpawnPlayerAtPosition(clientId);
            }
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

        UpdateSpawnPoints();
        int spawnIndex = GetSpawnIndex(clientId);
        Transform targetPoint = spawnPoints[spawnIndex % spawnPoints.Length];

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            // 기존 객체가 있다면? 위치만 옮겨주고 종료 (부활 처리)
            if (client.PlayerObject != null)
            {
                // NetworkTransform을 사용하는 경우 Teleport 기능을 사용해야 부드럽게 동기화됩니다.
                var networkTransform = client.PlayerObject.GetComponent<NetworkTransform>();
                if (networkTransform != null)
                {
                    networkTransform.Teleport(targetPoint.position, targetPoint.rotation, client.PlayerObject.transform.localScale);
                }
                else
                {
                    client.PlayerObject.transform.position = targetPoint.position;
                    client.PlayerObject.transform.rotation = targetPoint.rotation;
                }

                // 필요하다면 체력 리셋 로직도 여기서 호출
                // client.PlayerObject.GetComponent<PlayerStateManager>().ResetHealth();
                return;
            }
        }

        // 1. 이미 이 클라이언트를 위한 플레이어 객체가 있는지 확인 (중복 스폰 방지)
        //var clientData = NetworkManager.Singleton.ConnectedClients[clientId];
        //if (clientData.PlayerObject != null) return;

        //if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        //{
        //    if (client.PlayerObject != null)
        //    {
        //        // 이미 씬에 플레이어 객체가 있다면 다시 소환하지 않음
        //        return;
        //    }
        //}

        // 2. 이름으로 프리팹 찾기
        var networkPrefab = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs
            .FirstOrDefault(p => p.Prefab.name == playerPrefabName);

        if (networkPrefab == null || networkPrefab.Prefab == null)
        {
            Debug.LogError($"[GameSceneManager] 리스트에서 '{playerPrefabName}' 프리팹을 찾을 수 없습니다!");
            return;
        }

        UpdateSpawnPoints();


        // 스폰 포인트 정보 갱신 (없을 경우에만)
        if (spawnPoints == null || spawnPoints.Length == 0) UpdateSpawnPoints();

        //int spawnIndex = 0;
        var connectedList = NetworkManager.Singleton.ConnectedClientsList;

        for (int i = 0; i < connectedList.Count; i++)
        {
            if (connectedList[i].ClientId == clientId)
            {
                spawnIndex = i;
                break;
            }
        }

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // 포인트 개수보다 플레이어가 많으면 처음부터 순환 (Index % Length)
            //Transform targetPoint = spawnPoints[spawnIndex % spawnPoints.Length];
            Vector3 spawnPos = targetPoint.position;
            Quaternion spawnRot = targetPoint.rotation;

            GameObject playerObj = Instantiate(networkPrefab.Prefab, spawnPos, spawnRot);
            var networkObj = playerObj.GetComponent<NetworkObject>();
            networkObj.SpawnAsPlayerObject(clientId);

            if(spawnIndex == 0 || spawnIndex == 2)
            {
                playerObj.transform.rotation = Quaternion.Euler(0, 180f, 0);

                //var nt = networkObj.GetComponent<NetworkTransform>();
                //if (nt != null) nt.Teleport(spawnPos, Quaternion.Euler(0, 180f, 0), playerObj.transform.localScale);
            }

        }
        else
        {
            // 예외 상황: 스폰 포인트가 없을 때 기본 위치 생성
            GameObject playerObj = Instantiate(networkPrefab.Prefab, new Vector3(0, 1, 0), Quaternion.identity);
            playerObj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        }

        //Vector3 spawnPos = new Vector3(100f, 1f, 135f);
        //Quaternion spawnRot = Quaternion.identity;

        //// 4. 서버에서 인스턴스화 및 네트워크 스폰
        //GameObject playerObj = Instantiate(networkPrefab.Prefab, spawnPos, spawnRot);
        //var networkObj = playerObj.GetComponent<NetworkObject>();

        // 중요: 이 함수를 통해 해당 clientId가 이 객체의 주인임을 선언합니다.
        //networkObj.SpawnAsPlayerObject(clientId);

        //Debug.Log($"[GameSceneManager] 클라이언트 {clientId} 전용 플레이어 생성 및 스폰 완료");
    }

    // 로컬 클라이언트에서 씬 로드가 끝났을 때 실행됨
    private void OnLocalLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        // 내 로컬 캐릭터를 찾아서 UI를 다시 연결해줌
        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var interaction = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerInteraction>();
            if (interaction != null)
            {
                interaction.FindUIElements();
            }
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


    [ServerRpc(RequireOwnership = false)]
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