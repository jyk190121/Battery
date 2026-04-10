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
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask obstacleMask;

    // 현재 유효한 타겟 및 위치 정보
    public Transform CurrentTarget { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }
    public Vector3 LastHeardPosition { get; private set; }

    // 지능형 수색을 위한 타겟 속도 데이터
    public Vector3 LastTargetVelocity { get; private set; }
    private Vector3 previousTargetPos;

    // 가비지 컬렉션(GC) 방지를 위한 사전 할당 배열 및 경로 변수
    private Collider[] hitColliders = new Collider[10];
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
        // 1. 주변 플레이어 감지
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, data.viewRange, hitColliders, playerMask);

        Transform bestTarget = null;
        float minSqrDistance = float.MaxValue;
        GameObject potentialTargetObj = null;

        bool usingMemoryForTarget = false;

        for (int i = 0; i < hitCount; i++)
        {
            GameObject targetObj = hitColliders[i].gameObject;

            // 2단계: 유효성 검사 (안전구역 등)
            if (!IsTargetValid(targetObj)) continue;

            // 3단계: 높이(층수) 필터링 - 레이캐스트 전 1차 거름망
            float heightDiff = Mathf.Abs(targetObj.transform.position.y - transform.position.y);
            if (heightDiff > 5.0f) continue;

            // 4단계: 거리 체크 
            Vector3 diff = targetObj.transform.position - transform.position;
            float currentSqrDist = diff.sqrMagnitude;
            if (currentSqrDist > viewRangeSqr) continue;

            bool hasLOS = HasLineOfSight(targetObj.transform);
            bool isRemembered = false;

            if (hasLOS)
            {
                // 타겟이 실제로 보인다면 마지막 목격 시간 갱신
                if (CurrentTarget == null || targetObj.transform == CurrentTarget)
                {
                    timeLastSeen = Time.time;
                }
            }
            else
            {
                // 벽/문에 가려졌지만, 방금 전까지 쫓던 타겟이라면?
                if (CurrentTarget != null && targetObj.transform == CurrentTarget)
                {
                    // 기억 유효 시간(1.5초) 이내라면 강제로 시야에 있는 것으로 판정!
                    if (Time.time - timeLastSeen <= data.visionMemoryTime)
                    {
                        hasLOS = true;
                        isRemembered = true;
                    }
                }
            }
            if (hasLOS)
            {
                if (currentSqrDist < minSqrDistance)
                {
                    minSqrDistance = currentSqrDist;
                    potentialTargetObj = targetObj;
                    usingMemoryForTarget = isRemembered;
                }
            }
        }

        // 6단계: 도달 가능성 검사 (길이 너무 멀면 포기)
        if (potentialTargetObj != null)
        {
            if (usingMemoryForTarget || IsPathReasonable(potentialTargetObj.transform.position, minSqrDistance))
            {
                bestTarget = potentialTargetObj.transform;
            }
        }

        // 타겟 정보 및 속도 계산 업데이트
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
    private bool IsPathReasonable(Vector3 targetPos, float directSqrDist)
    {
        if (NavMesh.CalculatePath(transform.position, targetPos, NavMesh.AllAreas, path))
        {
            if (path.status != NavMeshPathStatus.PathComplete) return false;

            float pathLength = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                pathLength += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            // 직선거리보다 너무 많이 돌아가야 하면 부적절한 경로로 판단
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
}