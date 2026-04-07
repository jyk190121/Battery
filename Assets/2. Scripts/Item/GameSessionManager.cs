using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode; // 💡 네트워크 직렬화를 위해 추가됨

// 💡 [수정됨] 향후 클라이언트에게 UI 데이터 등을 전송할 때 에러가 나지 않도록
// 네트워크 통신에 최적화된 규격(INetworkSerializable)으로 업그레이드된 구조체입니다.
[System.Serializable]
public struct ItemSaveData : INetworkSerializable
{
    public int itemID;
    public Vector3 localPos;
    public Quaternion localRot;

    // 배열(float[]) 대신 단일 변수로 변경하여 네트워크 패킷 최적화
    public float stateValue1;

    public int slotIndex;

    // NGO 네트워크 통신용 포장(직렬화) 메서드 필수 구현
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
    // 멀티플레이 플레이어별 가방 보관소
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