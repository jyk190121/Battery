using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

// 몬스터의 감각(시각, 청각)을 담당하는 클래스
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
    public Vector3 LastHeardPosition { get; private set; } // 추후 소리 감지용 변수

    // 가비지 컬렉션(GC) 방지를 위한 사전 할당 배열
    // OverlapSphere 대신 NonAlloc을 사용하여 매 프레임 발생하는 메모리 쓰레기를 제거
    private Collider[] hitColliders = new Collider[10];
    private NavMeshPath path;

    private float viewRangeSqr;

    public void Init(MonsterController controller, MonsterData monsterData)
    {
        owner = controller;
        data = monsterData;
        path = new NavMeshPath();
        viewRangeSqr = data.viewRange * data.viewRange;
    }

    public void Tick()
    {
        // 1. 반경 내 모든 플레이어 추출
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, data.viewRange, hitColliders, playerMask);

        Transform bestTarget = null;
        float minSqrDistance = float.MaxValue; // 가장 가까운 타겟을 찾기 위한 비교값

        //foreach (var hit in hits)
        //{
        //    if (!IsTargetValid(hit.gameObject)) continue;

        //    // 2. 360도 시야: 각도 체크는 빼고, 장애물(벽)에 가려졌는지만 체크
        //    if (HasLineOfSight(hit.transform))
        //    {
        //        if (hit.transform.position.y - gameObject.transform.position.y > 5) { Debug.Log("범위에는 들어왔지만 높이가 다름"); return; }

        //        float dist = Vector3.Distance(transform.position, hit.transform.position);

        //        if (dist < minDistance)
        //        {
        //            minDistance = dist;
        //            bestTarget = hit.transform;
        //        }
        //    }
        //}

        //CurrentTarget = bestTarget;

        //// 타겟이 존재하면, 마지막 목격 위치를 계속 업데이트
        //if (CurrentTarget != null)
        //{
        //    LastSeenPosition = CurrentTarget.position;
        //}

        GameObject potentialTargetObj = null;

        for (int i = 0; i < hitCount; i++)
        {
            GameObject targetObj = hitColliders[i].gameObject;

            // 2단계: 유효성 검사 (안전구역 체크 등)
            if (!IsTargetValid(targetObj)) continue;

            // 3단계: 높이(층수) 필터링
            // 레이캐스트를 쏘기 전, 단순한 뺄셈으로 1층/2층 플레이어를 걸러냅니다.
            float heightDiff = Mathf.Abs(targetObj.transform.position.y - transform.position.y);
            if (heightDiff > 5.0f) continue;

            // 4단계: 거리 체크 
            Vector3 diff = targetObj.transform.position - transform.position;
            float currentSqrDist = diff.sqrMagnitude;

            // 미리 계산해둔 사거리 제곱값(viewRangeSqr)과 비교
            if (currentSqrDist > viewRangeSqr) continue;

            // 5단계: 장애물 체크
            // 여기까지 통과한 대상(가깝고, 같은 층에 있는 플레이어)에게만 인식
            if (HasLineOfSight(targetObj.transform))
            {
                // 6단계: 도달 가능성 검사 
                // 눈에는 보이지만 실제로 벽으로 막혀 돌아가는 길이 너무 멀면 추격하지 않습니다.
                //if (!IsPathReasonable(targetObj.transform.position, currentSqrDist)) continue;

                // 모든 필터를 통과한 대상 중 가장 가까운 타겟을 선정합니다.
                if (currentSqrDist < minSqrDistance)
                {
                    minSqrDistance = currentSqrDist;
                    potentialTargetObj = targetObj;
                }
            }
        }

        if (potentialTargetObj != null)
        {
            if (IsPathReasonable(potentialTargetObj.transform.position, minSqrDistance))
            {
                bestTarget = potentialTargetObj.transform;
            }
        }

        CurrentTarget = bestTarget;

        if (CurrentTarget != null)
        {
            LastSeenPosition = CurrentTarget.position;
        }
    }

    // [확장성] 나중에 소리 감지 시스템이 추가되었을 때 외부(SoundManager 등)에서 호출할 훅(Hook) 함수
    public void OnHeardSound(Vector3 soundOrigin, float noiseLevel)
    {
        // 거리에 따른 소리 감지 판정 (소리 크기 - 거리)
        float distance = Vector3.Distance(transform.position, soundOrigin);
        if (distance <= data.hearingRange * noiseLevel)
        {
            LastHeardPosition = soundOrigin;
            Debug.Log($"[{owner.name}] 소리를 감지했습니다! 위치: {soundOrigin}");

            // 현재 순찰 중이라면 수색(Search) 상태로 전환하여 소리가 난 곳으로 이동시킬 수 있음
            if (owner.CurrentStateNet.Value == MonsterStateType.Patrol ||
                owner.CurrentStateNet.Value == MonsterStateType.Idle)
            {
                // LastSeenPosition을 소리 위치로 덮어씌워 해당 위치를 조사하게 만듦
                LastSeenPosition = soundOrigin;
                owner.ChangeState(MonsterStateType.Search);
            }
        }
    }

    private bool IsTargetValid(GameObject target)
    {
        // 필요 시 안전구역 로직 추가
        return !owner.IsInSafeZone(target);
    }

    // NavMesh를 이용해 타겟까지의 실제 '보행 거리'가 합리적인지 판단
    private bool IsPathReasonable(Vector3 targetPos, float directSqrDist)
    {
        // 목적지까지의 경로 설계도를 그립니다.
        if (NavMesh.CalculatePath(transform.position, targetPos, NavMesh.AllAreas, path))
        {
            // 경로가 완전히 끊겨 있다면(예: 잠긴 문 너머 등) 무시 
            if (path.status != NavMeshPathStatus.PathComplete) return false;

            // 설계도상의 모든 코너 점들의 거리를 합산하여 실제 보행 거리를 구합니다.
            float pathLength = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                pathLength += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            // 실제 보행 거리가 시야 사거리보다 너무 길면(돌아가야 하면) 무시
            return pathLength < data.viewRange * 1.5f;
        }
        return false;
    }

    // 장애물 체크만 하는 함수 (360도 시야)
    private bool HasLineOfSight(Transform target)
    {
        Vector3 startPos = transform.position + (Vector3.up * 1.5f);
        Vector3 targetPos = target.position + (Vector3.up * 1.0f);

        Vector3 dir = (targetPos - startPos).normalized;
        float actualDist = Vector3.Distance(startPos, targetPos); // Raycast는 실제 거리가 필수

        // 레이가 장애물에 부딪히지 않아야 시야 확보됨
        return !Physics.Raycast(startPos, dir, actualDist, obstacleMask);
    }

    // 기즈모를 활용하여 시야 범위와 마지막 목격 위치 시각화
    private void OnDrawGizmos()
    {
        if (data == null) return;

        // 360도 탐지 반경 시각화 
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.viewRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.hearingRange); // 청각 범위 시각화

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