using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MonsterPool : MonoBehaviour
{
    public static MonsterPool Instance;

    // 프리팹별로 몬스터를 보관할 큐(Queue) 사전
    private Dictionary<GameObject, Queue<NetworkObject>> pool = new Dictionary<GameObject, Queue<NetworkObject>>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 창고에서 몬스터를 꺼내오거나, 없으면 새로 만듭니다. (서버 전용)
    /// </summary>
    public NetworkObject GetMonster(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!pool.ContainsKey(prefab))
        {
            pool[prefab] = new Queue<NetworkObject>();
        }

        // 창고에 남은 몬스터가 있다면? -> 재활용
        if (pool[prefab].Count > 0)
        {
            NetworkObject netObj = pool[prefab].Dequeue();
            netObj.transform.position = position;
            netObj.transform.rotation = rotation;
            netObj.gameObject.SetActive(true);

            // 클라이언트들에게 다시 알려줌
            netObj.Spawn();
            return netObj;
        }
        // 창고가 비어있다면? -> 새로 생성 (게임 초반에만 일어남)
        else
        {
            GameObject obj = Instantiate(prefab, position, rotation);
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            netObj.Spawn();
            return netObj;
        }
    }

    /// <summary>
    /// 죽은 몬스터를 파괴하지 않고 창고로 회수합니다. (서버 전용)
    /// </summary>
    public void ReturnMonster(GameObject prefab, NetworkObject netObj)
    {
        // false를 넣으면 클라이언트 화면에서는 파괴되지만 서버 메모리에는 남습니다
        netObj.Despawn(false);
        netObj.gameObject.SetActive(false); // 서버에서도 안 보이게 숨김

        if (!pool.ContainsKey(prefab))
        {
            pool[prefab] = new Queue<NetworkObject>();
        }
        pool[prefab].Enqueue(netObj);
    }
}