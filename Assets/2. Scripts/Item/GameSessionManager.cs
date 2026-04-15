using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public struct ItemSaveData : INetworkSerializable
{
    public int itemID;
    public Vector3 localPos;
    public Quaternion localRot;
    public float stateValue1;
    public int slotIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemID);
        serializer.SerializeValue(ref localPos);
        serializer.SerializeValue(ref localRot);
        serializer.SerializeValue(ref stateValue1);
        serializer.SerializeValue(ref slotIndex);
    }
}

/// <summary>
/// 게임 내 아이템, 플레이어 접속 상태, 다음 씬으로 가져갈 데이터들을 총괄합니다.
/// 게임 씬이 넘어가도 파괴되지 않으며, 서버(Host)가 주도적으로 통제합니다.
/// </summary>
public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance;

    [Header("Session State (세션 상태)")]
    [Tooltip("현재 방에 접속한 플레이어 중 사망한 사람의 수")]
    public int deadPlayersCount = 0;

    //연타 방지용 내부 자물쇠 변수
    private bool isStartSequenceActive = false;

    [Header("Session Data (보존 데이터)")]
    [Tooltip("트럭(정산 구역)에 저장된 아이템 목록")]
    public List<ItemSaveData> truckItems = new List<ItemSaveData>();

    [Tooltip("플레이어별 인벤토리에 보존된 아이템 목록 (Key: ClientID)")]
    public Dictionary<ulong, List<ItemSaveData>> playerItems = new Dictionary<ulong, List<ItemSaveData>>();

    [Tooltip("상점 구매 및 퀘스트 보상으로 다음 씬에서 트럭에 스폰될 아이템 ID 대기열")]
    public List<int> pendingSpawnItemIDs = new List<int>();

    [Header("Database (데이터베이스)")]
    [Tooltip("게임 내 존재하는 모든 아이템 프리팹 원본 목록")]
    public List<ItemBase> itemPrefabsDB = new List<ItemBase>();

  
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
        }
    }

    private void Start()
    {
        // 오직 서버(Host)만이 이 매니저를 네트워크에 등록(Spawn)할 수 있습니다.Host가 아니라면 자동생성되어야함.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (!NetworkObject.IsSpawned)
            {
                NetworkObject.Spawn(this);
                Debug.Log("<color=green>[GameSessionManager]</color> 서버 주도로 네트워크 스폰 완료.");
            }
        }
    }

    // ==========================================================
    // 생명주기 및 이벤트 등록 
    // ==========================================================
    public override void OnNetworkSpawn()
    {
        // 서버(방장)만 씬 로딩 완료 이벤트를 구독합니다.
        if (IsServer && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadComplete;
        }
    }

    public override void OnNetworkDespawn()
    {
        // 매니저 파괴 시 이벤트 구독을 해제하여 메모리 누수를 막습니다.
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadComplete;
        }
    }

    /// <summary>
    /// 씬 로딩이 완전히 끝났을 때 자동으로 호출됩니다.
    /// </summary>
    private void OnSceneLoadComplete(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        //  씬 이동이 끝났으므로 다음 출발을 위해 자물쇠를 풀어줍니다!
        isStartSequenceActive = false;

        Debug.Log($"<color=lime>[GameSessionManager]</color> {sceneName} 씬 로드 완료. 출발 자물쇠 해제.");
    }

    // ==========================================================
    // 외부 제공 유틸리티 (Utilities)
    // ==========================================================
    /// <summary>
    /// 현재 네트워크 방에 접속해 있는 실제 플레이어의 총 인원수를 반환합니다.
    /// </summary>
    public int GetTotalPlayers()
    {
        if (NetworkManager.Singleton != null)
            return NetworkManager.Singleton.ConnectedClientsIds.Count;
        return 1;
    }

    /// <summary>
    /// 아이템 ID를 입력받아 데이터베이스에서 해당 프리팹 원본을 찾아 반환합니다.
    /// </summary>
    public ItemBase GetPrefab(int targetItemID)
    {
        foreach (ItemBase item in itemPrefabsDB)
        {
            if (item != null && item.itemData != null && item.itemData.itemID == targetItemID)
                return item;
        }
        return null;
    }

    // ==========================================================
    // 시스템 제어 (System Control)
    // ==========================================================
    /// <summary>
    /// [로비/상점 전용] 결제를 요청하고 성공 시 다음 씬 스폰 대기열에 추가합니다.
    /// </summary>
    public void AddItemToSpawnQueue(int itemID, int price)
    {
        if (!IsServer) return;

        // 다른 매니저(GameMaster)에게 결제만 위임하여 결합도를 낮춤
        bool isPurchaseApproved = GameMaster.Instance != null && GameMaster.Instance.RequestPurchase(price);

        if (isPurchaseApproved)
        {
            pendingSpawnItemIDs.Add(itemID);
            Debug.Log($"<color=green>[Shop]</color> {itemID}번 아이템 결제 승인. 스폰 대기열 등록 완료.");
        }
        else
        {
            Debug.LogWarning($"<color=red>[Shop]</color> 잔액 부족. {itemID}번 아이템 결제 실패.");
        }
    }

    /// <summary>
    /// 새로운 세션(1일차) 시작 시 데이터베이스를 제외한 모든 임시 데이터를 초기화합니다.
    /// </summary>
    public void ResetSession()
    {
        isStartSequenceActive = false; // 시작 자물쇠 해제
        deadPlayersCount = 0;

        truckItems.Clear();
        playerItems.Clear();
        pendingSpawnItemIDs.Clear();

        Debug.Log("[GameSessionManager] 세션 임시 데이터 초기화 완료.");
    }

    // ==========================================================
    // 네트워크 통신 (RPCs)
    // ==========================================================
    /// <summary>
    /// [StartButton 호출] 누구나 서버에게 게임 씬으로의 이동을 요청할 수 있습니다.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestStartGameServerRpc(string targetSceneName, RpcParams rpcParams = default)
    {
        // 1. 보안 및 연타 방어
        if (!IsServer || isStartSequenceActive) return;

        isStartSequenceActive = true;
        Debug.Log($"<color=yellow>[GameSessionManager]</color> 시작 시퀀스 가동. 목적지: {targetSceneName}");

        // 2. 출발 전 짐 싸기 (의존성 분리: 퀘스트 관련은 QuestManager에게 위임)
        PrepareReturnQuestItems();

        // 3. 씬 이동 (모든 클라이언트 동기화)
        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(targetSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// 현재 활성화된 환원 퀘스트(Return)를 확인하여 필요한 아이템을 스폰 대기열에 넣습니다.
    /// </summary>
    private void PrepareReturnQuestItems()
    {
        if (QuestManager.Instance == null) return;

        foreach (int questID in QuestManager.Instance.activeQuests)
        {
            QuestDataSO questData = QuestManager.Instance.GetQuestData(questID);

            if (questData != null && questData.type == QuestType.Return)
            {
                pendingSpawnItemIDs.Add(questData.targetItemID);
                Debug.Log($"<color=green>[Quest]</color> 환원 퀘스트 지급품({questData.targetItemID}) 적재 완료.");
            }
        }
    }
}