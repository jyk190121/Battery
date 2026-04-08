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

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance;
    public int currentMoney = 0;

    [Header("Save Containers")]
    public List<ItemSaveData> truckItems = new List<ItemSaveData>();
    public Dictionary<ulong, List<ItemSaveData>> playerItems = new Dictionary<ulong, List<ItemSaveData>>();

    [Header("Prefab Database")]
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

    public void AddMoney(int amount)
    {
        currentMoney += amount;
        Debug.Log($"<color=yellow><b>[Money]</b> 정산 완료: +{amount}원 (현재: {currentMoney}원)</color>");
    }

    // 방이 깨졌을 때 이전 게임의 좀비 데이터를 방지하는 초기화 함수 (필요시 타이틀 화면에서 호출)
    public void ResetSession()
    {
        currentMoney = 0;
        truckItems.Clear();
        playerItems.Clear();
        Debug.Log("<b>[Session]</b> 게임 세션 데이터가 초기화되었습니다.");
    }

    public ItemBase GetPrefab(int id)
    {
        foreach (var item in itemPrefabsDB)
        {
            if (item == null) continue;
            if (item.itemData == null) continue;
            if (item.itemData.itemID == id) return item;
        }
        Debug.LogError($"🚨 [DB 에러] ID {id}번 아이템을 찾을 수 없습니다.");
        return null;
    }
}