using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 감각(시각, 청각) 및 타겟 추적을 담당하는 클래스
/// </summary>
public class EnvironmentScanner : MonoBehaviour
{
    public MonsterController owner;
    public MonsterData data;

    [Header("Detection Settings")]
    //[SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask obstacleMask;

    // 현재 유효한 타겟 및 위치 정보
    public Transform CurrentTarget { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }
    public Vector3 LastHeardPosition { get; private set; }

    // 지능형 수색을 위한 타겟 속도 데이터
    public Vector3 LastTargetVelocity { get; private set; }
    private Vector3 previousTargetPos;

    // 가비지 컬렉션(GC) 방지를 위한 사전 할당 배열 및 경로 변수
    //private Collider[] hitColliders = new Collider[10];
    private NavMeshPath path;
    private float viewRangeSqr;
    private float timeLastSeen = 0f;

    public void Init(MonsterController controller, MonsterData monsterData)
    {
        owner = controller;
        data = monsterData;
        path = new NavMeshPath();
        viewRangeSqr = data.viewRange * data.viewRange;
    }

    /// <summary>
    /// 매 프레임 혹은 지정된 Tick 주기에 감각 연산 수행
    /// </summary>
    public void Tick()
    {
        if (!owner.IsServer) return;

        Transform bestTarget = null;
        float minSqrDistance = float.MaxValue;

        // 현재 타겟이 있다면 우선권을 줍니다.
        float targetStickiness = 2.0f; // 현재 타겟은 2m 더 멀리 있어도 유지함

        foreach (PlayerController player in PlayerController.AllPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.isDead.Value) continue;
            if (!IsTargetValid(player.gameObject)) continue;

            Vector3 diff = player.transform.position - transform.position;
            float currentSqrDist = diff.sqrMagnitude;

            // 현재 타겟이라면 거리 판정을 더 후하게 줌 (타겟 고정 효과)
            if (CurrentTarget != null && player.transform == CurrentTarget)
            {
                currentSqrDist -= (targetStickiness * targetStickiness);
            }

            if (currentSqrDist > viewRangeSqr) continue;

            bool hasLOS = HasLineOfSight(player.transform);

            // 시야에서 잠깐 사라져도 기억함
            if (!hasLOS && CurrentTarget != null && player.transform == CurrentTarget)
            {
                if (Time.time - timeLastSeen <= data.visionMemoryTime)
                {
                    hasLOS = true;
                }
            }

            if (hasLOS)
            {
                if (IsPathReasonable(player.transform.position))
                {
                    if (currentSqrDist < minSqrDistance)
                    {
                        minSqrDistance = currentSqrDist;
                        bestTarget = player.transform;
                    }
                }
            }
        }

        if (bestTarget != null && bestTarget != CurrentTarget)
        {
            timeLastSeen = Time.time;
        }

        UpdateTargetData(bestTarget);
    }

    /// <summary>
    /// 타겟의 상태에 따라 위치와 예측 속도를 업데이트하는 로직 
    /// </summary>
    private void UpdateTargetData(Transform newTarget)
    {
        CurrentTarget = newTarget;

        if (CurrentTarget != null)
        {
            // 타겟 위치 갱신
            Vector3 currentPos = CurrentTarget.position;
            LastSeenPosition = currentPos;

            // 속도 계산 (예측 수색용)
            if (previousTargetPos != Vector3.zero)
            {
                float dt = Time.deltaTime;
                if (dt > 0) // 나누기 0 방지
                {
                    LastTargetVelocity = (currentPos - previousTargetPos) / dt;
                }
            }
            previousTargetPos = currentPos;
        }
        else
        {
            // 타겟을 놓친 경우 이전 위치 데이터 초기화 
            previousTargetPos = Vector3.zero;
        }
    }



    /// <summary>
    /// 소리 감지 훅 함수: 외부 SoundManager 등에서 호출
    /// </summary>
    public void OnHeardSound(Vector3 soundOrigin, float noiseLevel)
    {
        float distance = Vector3.Distance(transform.position, soundOrigin);
        if (distance <= data.hearingRange * noiseLevel)
        {
            LastHeardPosition = soundOrigin;
            Debug.Log($"[{owner.name}] 소리 감지: {soundOrigin}");

            // 순찰/정지 상태일 때 소리가 나면 즉시 수색 모드 진입
            if (owner.CurrentStateNet.Value == MonsterStateType.Patrol ||
                owner.CurrentStateNet.Value == MonsterStateType.Idle)
            {
                LastSeenPosition = soundOrigin; // 소리 위치를 수색 목표로 설정
                owner.ChangeState(MonsterStateType.Search);
            }
        }
    }

    private bool IsTargetValid(GameObject target) => !owner.IsInSafeZone(target);

    /// <summary>
    /// NavMesh를 이용해 타겟까지의 실제 보행 거리가 시야 범위 내인지 확인
    /// </summary>
    private bool IsPathReasonable(Vector3 targetPos)
    {
        if (NavMesh.CalculatePath(transform.position, targetPos, NavMesh.AllAreas, path))
        {
            if (path.status != NavMeshPathStatus.PathComplete) return false;

            float pathLength = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                pathLength += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            return pathLength < data.viewRange * 1.5f;
        }
        return false;
    }

    /// <summary>
    /// 레이캐스트를 이용한 시야 가림 여부 확인
    /// </summary>
    private bool HasLineOfSight(Transform target)
    {
        Vector3 startPos = transform.position + (Vector3.up * 1.5f); // 몬스터 눈높이
        Vector3 targetPos = target.position + (Vector3.up * 1.0f);   // 플레이어 가슴 높이

        Vector3 dir = (targetPos - startPos).normalized;
        float actualDist = Vector3.Distance(startPos, targetPos);

        // 장애물 레이어에 부딪히면 시야 차단
        return !Physics.Raycast(startPos, dir, actualDist, obstacleMask);
    }

    private void OnDrawGizmos()
    {
        if (data == null) return;

        // 시각 및 청각 범위 시각화
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.viewRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.hearingRange);

        // 마지막 목격 위치 시각화
        if (LastSeenPosition != Vector3.zero)
        {
            Gizmos.color = (Time.time - timeLastSeen <= data.visionMemoryTime) ? new Color(1f, 0.5f, 0f) : Color.yellow;
            Gizmos.DrawSphere(LastSeenPosition, 0.5f);
            Gizmos.DrawLine(transform.position, LastSeenPosition);
        }
    }

    /// <summary>
    /// [특수 기믹용] 외부 상태(CeilingWait 등)에서 특정 타겟을 강제로 고정할 때 사용합니다.
    /// </summary>
    public void SetForceTarget(Transform newTarget)
    {
        UpdateTargetData(newTarget);
        string targetName = newTarget != null ? newTarget.name : "None (초기화됨)";
        Debug.Log($"[{owner.name}] 타겟 강제 고정: {targetName}");
    }
}