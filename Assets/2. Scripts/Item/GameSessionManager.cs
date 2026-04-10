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
    public int currentMoney = 0;

    public int totalPlayersInSession = 4;
    public int deadPlayersCount = 0;

    public List<ItemSaveData> truckItems = new List<ItemSaveData>();
    public Dictionary<ulong, List<ItemSaveData>> playerItems = new Dictionary<ulong, List<ItemSaveData>>();
    public List<ItemBase> itemPrefabsDB = new List<ItemBase>();

    //  상점 구매템 + 환원 퀘스트 지급템을 담아갈 택배 리스트
    public List<int> pendingSpawnItemIDs = new List<int>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    //  구매시 잔액 계산 함수
    public void AddItemToSpawnQueue(int itemID, int price)
    {
        if (currentMoney >= price)
        {
            currentMoney -= price;
            pendingSpawnItemIDs.Add(itemID);
            Debug.Log($"[Shop] {itemID}번 아이템 구매 완료! (현재 잔액: {currentMoney})");
        }
    }

    public void ProcessFinalSettlement(int totalScrapValue, int recoveredPhonesCount)
    {
        if (!IsServer) return;

        if (deadPlayersCount >= totalPlayersInSession)
        {
            Debug.Log("<color=red><b>[System] 팀 전멸! 1일차로 리셋됩니다.</b></color>");
            ResetSession();
            NetworkManager.SceneManager.LoadScene("GameOverScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            return;
        }

        int questIncome = QuestManager.Instance.GetCalculatedQuestReward();
        int grossIncome = totalScrapValue + questIncome;

        int missingPhones = Mathf.Max(0, deadPlayersCount - recoveredPhonesCount);
        float penaltyMultiplier = 1.0f - (missingPhones * 0.05f);

        int finalNetIncome = Mathf.RoundToInt(grossIncome * penaltyMultiplier);
        AddMoney(finalNetIncome);

        Debug.Log($"[정산 완료] 폐지 {totalScrapValue} + 퀘스트 {questIncome} | 미회수 폰 {missingPhones}개(-{missingPhones * 5}%) => 입금액: {finalNetIncome}원");

        NetworkManager.SceneManager.LoadScene("LobbyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void AddMoney(int amount)
    {
        currentMoney += amount;
    }

    public void ResetSession()
    {
        currentMoney = 0;
        truckItems.Clear();
        playerItems.Clear();
        deadPlayersCount = 0;
        pendingSpawnItemIDs.Clear(); //초기화 시 스폰 대기열도 비움
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