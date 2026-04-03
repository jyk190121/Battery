using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ItemSpawner : MonoBehaviour
{
    public Transform[] spawnPoints; // 씬 곳곳에 배치한 Empty Object들
    public GameObject[] itemPrefabs; // GameSessionManager의 DB와 일치하는 프리팹들
    public int spawnCount = 10; // 이번 판에 스폰할 아이템 개수

    void Start()
    {
        SpawnRandomItems();
    }

    void SpawnRandomItems()
    {
        // 스폰 지점이 부족하면 개수 조절
        int finalCount = Mathf.Min(spawnCount, spawnPoints.Length);

        // 위치를 무작위로 섞기 (중복 스폰 방지)
        List<Transform> availablePoints = new List<Transform>(spawnPoints);

        for (int i = 0; i < finalCount; i++)
        {
            int pointIdx = Random.Range(0, availablePoints.Count);
            int itemIdx = Random.Range(0, itemPrefabs.Length);

            Instantiate(itemPrefabs[itemIdx], availablePoints[pointIdx].position, Quaternion.identity);
            availablePoints.RemoveAt(pointIdx); // 사용한 지점 제거
        }
    }
}