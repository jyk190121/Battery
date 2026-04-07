using UnityEngine;
using Unity.Netcode;

public class EnvironmentScanner : MonoBehaviour
{
    public MonsterController owner;
    public MonsterData data;

    [Header("Detection Settings")]
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask obstacleMask;

    // 현재 유효한 타겟
    public Transform CurrentTarget { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }

    public void Init(MonsterController controller, MonsterData monsterData)
    {
        owner = controller;
        data = monsterData;
    }

    public void Tick()
    {
        // 1. 반경 내 모든 플레이어 추출
        Collider[] hits = Physics.OverlapSphere(transform.position, data.viewRange, playerMask);

        Transform bestTarget = null;
        float minDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!IsTargetValid(hit.gameObject)) continue;

            // 2. 360도 시야: 각도 체크는 빼고, 장애물(벽)에 가려졌는지만 체크
            if (HasLineOfSight(hit.transform))
            {
                if (hit.transform.position.y - gameObject.transform.position.y > 5) { Debug.Log("범위에는 들어왔지만 높이가 다름"); return; }

                float dist = Vector3.Distance(transform.position, hit.transform.position);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = hit.transform;
                }
            }
        }

        CurrentTarget = bestTarget;

        // 타겟이 존재하면, 마지막 목격 위치를 계속 업데이트
        if (CurrentTarget != null)
        {
            LastSeenPosition = CurrentTarget.position;
        }
    }

    private bool IsTargetValid(GameObject target)
    {
        // 필요 시 안전구역 로직 추가
        return true;
    }

    // 장애물 체크만 하는 함수 (360도 시야)
    private bool HasLineOfSight(Transform target)
    {
        Vector3 dir = (target.position - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, target.position);

        // 플레이어를 향해 레이저를 쏴서 장애물이 있는지 확인
        if (Physics.Raycast(transform.position + Vector3.up, dir, dist, obstacleMask))
        {
            return false; // 벽에 가려짐
        }

        return true; // 시야 확보됨
    }

    // 기즈모를 활용하여 시야 범위와 마지막 목격 위치 시각화
    private void OnDrawGizmos()
    {
        if (data == null) return;

        // 360도 탐지 반경 시각화 
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.viewRange);

        // 마지막으로 본 위치 시각화 
        if (LastSeenPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(LastSeenPosition, 0.5f);

            // 몬스터에서 마지막 목격 위치까지 점선 긋기
            Gizmos.DrawLine(transform.position, LastSeenPosition);
        }
    }
}