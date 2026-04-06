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
        // 1. 주변 플레이어 수집
        Collider[] hits = Physics.OverlapSphere(transform.position, data.viewRange, playerMask);

        Transform bestTarget = null;
        float minDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            // [중요] 팀원의 플레이어 스크립트에서 "건물 안 여부"를 체크한다고 가정
            // 예: hit.GetComponent<PlayerStatus>().isInsideBuilding
            if (!IsTargetValid(hit.gameObject)) continue;

            // 2. 시야각 및 장애물 체크 (이전 로직 활용)
            if (IsInVisualCone(hit.transform))
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = hit.transform;
                }
            }
        }

        CurrentTarget = bestTarget;
        if (CurrentTarget != null) LastSeenPosition = CurrentTarget.position;
    }

    private bool IsTargetValid(GameObject target)
    {
        // 1. 안전구역에 있는가?
        //if (owner.IsInSafeZone(target)) return false;

        // 2. 건물 밖에 있는가? (팀원과 협의할 변수)
        // 만약 플레이어 스크립트에 접근하기 어렵다면, 
        // 여기서 직접 Physics.CheckSphere 등을 이용해 "Building" 레이어 안에 있는지 확인 가능합니다.
        return true;
    }

    private bool IsInVisualCone(Transform target)
    {
        Vector3 dir = (target.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dir) < data.viewAngle / 2)
        {
            return !Physics.Raycast(transform.position + Vector3.up, dir, data.viewRange, obstacleMask);
        }
        return false;
    }
}