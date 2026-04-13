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

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance;


    public int totalPlayersInSession = 4;
    public int deadPlayersCount = 0;


    public List<ItemSaveData> truckItems = new List<ItemSaveData>();
    public Dictionary<ulong, List<ItemSaveData>> playerItems = new Dictionary<ulong, List<ItemSaveData>>();
    public List<ItemBase> itemPrefabsDB = new List<ItemBase>();

    // 상점 구매템 + 환원 퀘스트 지급템을 담아갈 택배 리스트
    public List<int> pendingSpawnItemIDs = new List<int>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    // 상점 구매 로직: 내 지갑이 아니라 GameMaster(신용한도)에게 결제 요청
    public void AddItemToSpawnQueue(int itemID, int price)
    {
        if (!IsServer) return;

        // GameMaster를 통해 EconomyManager의 '남은 신용 한도'로 결제가 되는지 확인
        if (GameMaster.Instance != null && GameMaster.Instance.RequestPurchase(price))
        {
            pendingSpawnItemIDs.Add(itemID);
            Debug.Log($"<color=green>[Shop]</color> {itemID}번 아이템 구매 승인! (스폰 대기열 등록)");
        }
        else
        {
            Debug.LogWarning($"<color=red>[Shop]</color> 신용 한도가 부족하여 {itemID}번 아이템을 결제할 수 없습니다.");
        }
    }

    
    // 세션 리셋: 돈은 건드리지 않고, 오직 아이템/플레이어 상태만 초기화
    public void ResetSession()
    {
        truckItems.Clear();
        playerItems.Clear();
        deadPlayersCount = 0;
        pendingSpawnItemIDs.Clear();
        Debug.Log("[GameSessionManager] 아이템 및 스폰 데이터 초기화 완료.");
    }

    public ItemBase GetPrefab(int id)
    {
        foreach (var item in itemPrefabsDB)
        {
            if (item != null && item.itemData != null && item.itemData.itemID == id) return item;
        }
        return null;
    }
}