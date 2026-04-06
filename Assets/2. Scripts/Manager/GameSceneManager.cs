using System.Collections;
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
    public string playerPrefabName = "Player_KJY"; // 사용할 플레이어 프리팹 이름

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


    //public override void OnNetworkSpawn()
    //{
    //    // 서버(호스트)에서만 씬 로드 완료 이벤트를 감시합니다.
    //    if (IsServer)
    //    {
    //        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnAllClientsLoaded;
    //    }
    //}
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
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
        if (SceneManager.GetActiveScene().name == "KJY_Lobby")
        {
            Debug.Log($"[GameSceneManager] 클라이언트 {clientId} 접속 감지. 스폰 시도.");
            SpawnPlayerAtPosition(clientId);
        }
    }
    void OnSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "KJY_Lobby")
        {
            Debug.Log("[GameSceneManager] 로비 씬 로드 완료. 현재 접속된 모든 인원 스폰 시도.");
            foreach (var clientId in clientsCompleted)
            {
                SpawnPlayerAtPosition(clientId);
            }
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
        //// 서버가 해당 클라이언트를 위해 플레이어 생성
        //// 만약 NetworkManager에 PlayerPrefab이 등록되어 있다면:
        //NetworkManager.Singleton.GetNetworkPrefabOverride(NetworkManager.Singleton.NetworkConfig.PlayerPrefab);

        //// 로딩 완료 후 해당 위치로 '텔레포트' 시키는 방식이 가장 에러가 적습니다.

        //if (!IsServer) return;

        //// 1. 이미 스폰된 플레이어 객체가 있는지 확인
        //var client = NetworkManager.Singleton.ConnectedClients[clientId];
        //GameObject playerObj = client.PlayerObject != null ? client.PlayerObject.gameObject : null;

        //// 정해진 스폰 위치
        //Vector3 spawnPos = new Vector3(40.14f, 0.66f, 67.41f);
        //Quaternion spawnRot = Quaternion.identity;

        //// 2. 만약 플레이어 오브젝트가 없다면 새로 생성 (수동 스폰)
        //if (playerObj == null)
        //{
        //    // NetworkManager에 등록된 기본 플레이어 프리팹을 가져와서 서버에서 생성
        //    playerObj = Instantiate(NetworkManager.Singleton.NetworkConfig.PlayerPrefab, spawnPos, spawnRot);

        //    // 중요: 네트워크 상에 해당 클라이언트 소유로 스폰함을 선언
        //    var networkObj = playerObj.GetComponent<NetworkObject>();
        //    networkObj.SpawnAsPlayerObject(clientId);

        //    Debug.Log($"[GameSceneManager] {clientId}번 클라이언트 플레이어 신규 생성 완료");
        //}
        //else
        //{
        //    // 3. 이미 플레이어 객체가 있다면 해당 위치로 텔레포트
        //    // NetworkTransform이 붙어있다면 서버에서 위치만 바꿔주면 동기화됩니다.
        //    playerObj.transform.position = spawnPos;
        //    playerObj.transform.rotation = spawnRot;

        //    Debug.Log($"[GameSceneManager] {clientId}번 클라이언트 플레이어 위치 재설정 완료");
        //}

        if (!IsServer) return;

        // 1. 이미 이 클라이언트를 위한 플레이어 객체가 있는지 확인 (중복 스폰 방지)
        var clientData = NetworkManager.Singleton.ConnectedClients[clientId];
        if (clientData.PlayerObject != null)
        {
            Debug.Log($"[GameSceneManager] 클라이언트 {clientId}는 이미 플레이어 객체가 존재합니다.");
            return;
        }

        // 2. 이름으로 프리팹 찾기
        var networkPrefab = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs
            .FirstOrDefault(p => p.Prefab.name == playerPrefabName);

        if (networkPrefab == null || networkPrefab.Prefab == null)
        {
            Debug.LogError($"[GameSceneManager] 리스트에서 '{playerPrefabName}' 프리팹을 찾을 수 없습니다!");
            return;
        }

        // 3. 스폰 위치 설정 (동일 위치면 겹치므로 필요시 약간의 오프셋을 줄 수 있습니다)
        Vector3 spawnPos = new Vector3(100f, 0, 140f);
        Quaternion spawnRot = Quaternion.identity;

        // 4. 서버에서 인스턴스화 및 네트워크 스폰
        GameObject playerObj = Instantiate(networkPrefab.Prefab, spawnPos, spawnRot);
        var networkObj = playerObj.GetComponent<NetworkObject>();

        // 중요: 이 함수를 통해 해당 clientId가 이 객체의 주인임을 선언합니다.
        networkObj.SpawnAsPlayerObject(clientId);

        Debug.Log($"[GameSceneManager] 클라이언트 {clientId} 전용 플레이어 생성 및 스폰 완료");
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

    //public string SceneName()
    //{
    //    return SceneManager.GetActiveScene().name;
    //}
}