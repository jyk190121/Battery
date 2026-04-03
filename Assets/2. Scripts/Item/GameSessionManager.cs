using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ItemSaveData
{
    public int itemID;
    public Vector3 localPos;
    public Quaternion localRot;
    public float[] stateValues; // [0]: 내구도 등
    public int slotIndex;       // 인벤토리 위치 (-1이면 바닥)
}

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance;
    public int currentMoney = 0;

    [Header("Save Containers")]
    public List<ItemSaveData> truckItems = new List<ItemSaveData>();
    public List<ItemSaveData> playerItems = new List<ItemSaveData>();

    [Header("Prefab Database")]
    public List<ItemBase> itemPrefabsDB = new List<ItemBase>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    public void AddMoney(int amount)
    {
        currentMoney += amount;
        Debug.Log($"<color=yellow><b>[Money]</b> 정산 완료: +{amount}원 (현재: {currentMoney}원)</color>");
    }

    public ItemBase GetPrefab(int id)
    {
        foreach (var item in itemPrefabsDB)
        {
            if (item == null)
            {
                Debug.LogWarning("🚨 [DB 경고] 리스트에 빈칸(None)이 있습니다!");
                continue;
            }
            if (item.itemData == null)
            {
                Debug.LogWarning($"🚨 [DB 경고] 프리팹 '{item.gameObject.name}'에 ItemDataSO가 없습니다!");
                continue;
            }
            if (item.itemData.itemID == id) return item;
        }
        Debug.LogError($"🚨 [DB 에러] ID {id}번 아이템을 찾을 수 없습니다.");
        return null;
    }
}