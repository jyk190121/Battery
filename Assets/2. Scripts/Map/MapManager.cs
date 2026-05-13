using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class MapOManager : NetworkBehaviour
{
    [Header("장애물 설정")]
    [Tooltip("맵에 미리 배치해 둔 모든 장애물 오브젝트들을 여기에 넣습니다.")]
    public GameObject[] potentialObstacles;

    [Tooltip("매 판 활성화할 장애물의 갯수")]
    public int obstacleCountToActivate = 10;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GenerateRandomMap();
        }
    }

    private void GenerateRandomMap()
    {
        foreach (var obs in potentialObstacles)
        {
            obs.SetActive(false);
        }

        // 활성화할 인덱스를 뽑습니다. (중복 방지를 위해 리스트 사용)
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < potentialObstacles.Length; i++) availableIndices.Add(i);

        List<int> selectedIndices = new List<int>();
        int count = Mathf.Min(obstacleCountToActivate, potentialObstacles.Length);

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedObsIndex = availableIndices[randomIndex];

            selectedIndices.Add(selectedObsIndex);
            availableIndices.RemoveAt(randomIndex); // 중복 방지
        }

        // 2. 서버가 고른 번호들을 배열로 만들어서 모든 클라이언트에게 전송
        SyncObstaclesClientRpc(selectedIndices.ToArray());
    }

    [ClientRpc]
    private void SyncObstaclesClientRpc(int[] activeIndices)
    {
        // 3. 서버의 명령을 받은 모든 클라이언트(나 포함)가 동일한 장애물만 딱 켭니다
        foreach (int index in activeIndices)
        {
            if (index >= 0 && index < potentialObstacles.Length)
            {
                potentialObstacles[index].SetActive(true);
            }
        }

        Debug.Log($"<color=green>[Map]</color> {activeIndices.Length}개의 무작위 장애물이 배치되었습니다!");
    }
}