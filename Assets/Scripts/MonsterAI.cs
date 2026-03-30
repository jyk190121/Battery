using UnityEngine;
using UnityEngine.AI; // AI 기능을 쓰기 위해 필수!

public class MonsterAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform playerTransform;

    void Start()
    {
        // 몬스터에 붙어있는 NavMeshAgent를 가져옵니다.
        agent = GetComponent<NavMeshAgent>();

        // "Player" 태그를 가진 오브젝트를 찾아 추적 대상으로 정합니다.
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    void Update()
    {
        // 플레이어가 존재한다면 매 프레임마다 플레이어 위치를 목적지로 갱신합니다.
        if (playerTransform != null)
        {
            agent.SetDestination(playerTransform.position);
        }
    }
}