using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 감각(시각, 청각) 및 타겟 추적을 담당하는 핵심 AI 스캐너 클래스입니다.
/// 수직 지형(계단)에서의 시야 소실 버그를 막기 위해 XZ 평면 투영 및 다중 레이캐스트가 적용되었습니다.
/// </summary>
public class EnvironmentScanner : MonoBehaviour
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    [Header("--- References ---")]
    [Tooltip("이 스캐너를 소유하고 있는 몬스터 본체 컨트롤러")]
    public MonsterController owner;
    [Tooltip("몬스터의 시야/청각 스탯이 담긴 데이터")]
    public MonsterData data;

    [Header("--- Environment Settings ---")]
    [Tooltip("이 몬스터가 주로 활동하는 공간이 실내인가? (야외 몹이면 false)")]
    public bool isIndoorMonster = true;

    [Header("--- Detection Settings ---")]
    [Tooltip("시야가 가려졌는지 판단할 장애물 레이어")]
    [SerializeField] private LayerMask _obstacleMask;

    // [프로퍼티] 외부에서 읽기만 가능한 타겟 및 위치 정보
    public Transform CurrentTarget { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }
    public Vector3 LastHeardPosition { get; private set; }
    public Vector3 LastTargetVelocity { get; private set; }

    private Vector3 _previousTargetPos;
    private NavMeshPath _path;
    private float _viewRangeSqr;
    private float _timeLastSeen = 0f;

    // 길찾기 연산(CPU 폭탄) 캐싱용 딕셔너리
    private Dictionary<Transform, float> _lastPathCheckTimes = new Dictionary<Transform, float>();
    private Dictionary<Transform, bool> _cachedPathResults = new Dictionary<Transform, bool>();
    private float _pathCheckInterval = 0.5f;


    // =========================================================
    // 2. 초기화 함수
    // =========================================================

    public void Init(MonsterController controller, MonsterData monsterData)
    {
        owner = controller;
        data = monsterData;
        _path = new NavMeshPath();

        _viewRangeSqr = data.viewRange * data.viewRange;
    }

    private void OnEnable()
    {
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.RegisterScanner(this);
    }

    private void OnDisable()
    {
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.UnregisterScanner(this);
    }


    // =========================================================
    // 3. 유니티 루프 및 콜백 (시각적 디버깅)
    // =========================================================

    private void OnDrawGizmos()
    {
        if (data == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.hearingRange);

        Gizmos.color = Color.red;
        // 씬 뷰에서 2D 원기둥 시야를 직관적으로 확인할 수 있도록 Y축을 평평하게 그립니다.
        Vector3 flatForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 rightViewDir = Quaternion.Euler(0, data.viewAngle * 0.5f, 0) * flatForward;
        Vector3 leftViewDir = Quaternion.Euler(0, -data.viewAngle * 0.5f, 0) * flatForward;

        Gizmos.DrawRay(transform.position, rightViewDir * data.viewRange);
        Gizmos.DrawRay(transform.position, leftViewDir * data.viewRange);

        if (LastSeenPosition != Vector3.zero)
        {
            Gizmos.color = (Time.time - _timeLastSeen <= data.visionMemoryTime) ? new Color(1f, 0.5f, 0f) : Color.yellow;
            Gizmos.DrawSphere(LastSeenPosition, 0.5f);
            Gizmos.DrawLine(transform.position, LastSeenPosition);
        }
    }


    // =========================================================
    // 4. 퍼블릭 함수
    // =========================================================

    public void Tick()
    {
        if (!owner.IsServer) return;

        Transform bestTarget = null;
        float minSqrDistance = float.MaxValue;
        float targetStickiness = 2.0f;

        foreach (PlayerController player in PlayerController.AllPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.isDead.Value) continue;
            if (!IsTargetValid(player.gameObject)) continue;

            Vector3 diff = player.transform.position - transform.position;
            float currentSqrDist = diff.sqrMagnitude;

            if (CurrentTarget != null && player.transform == CurrentTarget)
            {
                currentSqrDist -= (targetStickiness * targetStickiness);
            }

            // 1. 3D 거리 검사: 상하좌우 무관하게 너무 멀면 아예 안 보임
            if (currentSqrDist > _viewRangeSqr) continue;

            // =================================================================
            // [Portfolio 최적화: 계단 버그 해결을 위한 XZ 평면 2D 시야각 판정]
            // 플레이어가 계단 아래/위에 있어서 상하 각도가 극심하게 꺾이더라도, 
            // 좌우 방향만 몬스터의 시야각 안에 있다면 포착되도록 Y축을 배제합니다.
            // =================================================================
            Vector3 forward2D = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            Vector3 dirToPlayer2D = new Vector3(diff.x, 0, diff.z).normalized;

            float angleToPlayer = Vector3.Angle(forward2D, dirToPlayer2D);

            bool isCloseEnoughToFeel = currentSqrDist <= (2.0f * 2.0f);

            // 좌우 각도를 벗어났고, 바짝 붙은 것도 아니라면 패스!
            if (angleToPlayer > data.viewAngle * 0.5f && !isCloseEnoughToFeel)
            {
                continue;
            }

            // 3. 시야 가림(벽 등 장애물) 다중 레이캐스트 검사
            bool hasLOS = HasLineOfSight(player.transform);

            if (!hasLOS && CurrentTarget != null && player.transform == CurrentTarget)
            {
                if (Time.time - _timeLastSeen <= data.visionMemoryTime)
                {
                    hasLOS = true; // 기억력 유지
                }
            }

            if (hasLOS)
            {
                if (IsPathReasonable(player.transform))
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
            _timeLastSeen = Time.time;
        }

        UpdateTargetData(bestTarget);
    }

    public void OnHeardSound(Vector3 soundOrigin, float noiseLevel, bool soundIsInside)
    {
        if (this.isIndoorMonster != soundIsInside) return;

        float verticalDifference = Mathf.Abs(transform.position.y - soundOrigin.y);

        if (verticalDifference >= 6.5f)
        {
            noiseLevel *= 0.3f;
            if (verticalDifference >= 13f) return;
        }

        Vector3 dirToSound = soundOrigin - transform.position;
        float distToSound = dirToSound.magnitude;

        Vector3 checkStart = transform.position + (Vector3.up * 1.5f);
        if (Physics.Raycast(checkStart, dirToSound.normalized, distToSound, _obstacleMask))
        {
            noiseLevel *= 0.5f;
        }

        float finalHearingRadius = data.hearingRange * noiseLevel;

        if (distToSound <= finalHearingRadius)
        {
            LastHeardPosition = soundOrigin;
            //Debug.Log($"<color=yellow>[소리 감지]</color> {owner.name}이(가) 소리를 들었습니다. (최종 반경: {finalHearingRadius:F1}m)");

            if (owner.CurrentStateNet.Value == MonsterStateType.Patrol ||
                owner.CurrentStateNet.Value == MonsterStateType.Idle ||
                owner.CurrentStateNet.Value == MonsterStateType.Search)
            {
                LastSeenPosition = soundOrigin;
                owner.ChangeState(MonsterStateType.Investigate);
            }
        }
    }

    public void SetForceTarget(Transform newTarget)
    {
        UpdateTargetData(newTarget);
    }


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    private void UpdateTargetData(Transform newTarget)
    {
        CurrentTarget = newTarget;

        if (CurrentTarget != null)
        {
            Vector3 currentPos = CurrentTarget.position;
            LastSeenPosition = currentPos;

            if (_previousTargetPos != Vector3.zero)
            {
                float dt = Time.deltaTime;
                if (dt > 0) LastTargetVelocity = (currentPos - _previousTargetPos) / dt;
            }
            _previousTargetPos = currentPos;
        }
        else
        {
            _previousTargetPos = Vector3.zero;
        }
    }

    private bool IsTargetValid(GameObject target)
    {
        if (owner.IsInSafeZone(target)) return false;

        if (target.TryGetComponent<PlayerController>(out var player) && player.isDead.Value)
        {
            return false;
        }

        return true;
    }

    private bool IsPathReasonable(Transform target)
    {
        if (_lastPathCheckTimes.TryGetValue(target, out float lastCheckTime))
        {
            if (Time.time - lastCheckTime < _pathCheckInterval) return _cachedPathResults[target];
        }

        bool isValid = false;
        if (NavMesh.CalculatePath(transform.position, target.position, NavMesh.AllAreas, _path))
        {
            if (_path.status == NavMeshPathStatus.PathComplete)
            {
                float pathLength = 0f;
                for (int i = 1; i < _path.corners.Length; i++)
                {
                    pathLength += Vector3.Distance(_path.corners[i - 1], _path.corners[i]);
                }
                isValid = pathLength < data.viewRange * 1.5f;
            }
        }

        _lastPathCheckTimes[target] = Time.time;
        _cachedPathResults[target] = isValid;

        return isValid;
    }

    /// <summary>
    /// [Portfolio 핵심 기술: 다중 타겟 부위 레이캐스트]
    /// 기존의 단일 레이캐스트는 계단 모서리에 닿으면 플레이어를 못 본 것으로 오판했습니다.
    /// 플레이어의 머리, 가슴, 다리 3곳으로 레이저를 발사하여 하나라도 통과하면 시야가 확보된 것으로 간주합니다.
    /// </summary>
    private bool HasLineOfSight(Transform target)
    {
        Vector3 startPos = transform.position + (Vector3.up * 1.5f); // 몬스터의 눈(카메라) 위치

        // 검사할 타겟의 3가지 부위 (머리, 가슴, 발)
        Vector3[] targetPoints = new Vector3[]
        {
            target.position + (Vector3.up * 1.6f), // 머리 (계단 아래로 내려갈 때 가장 끝까지 보임)
            target.position + (Vector3.up * 1.0f), // 가슴 (기본)
            target.position + (Vector3.up * 0.2f)  // 발 (장애물 밑 틈새로 보일 때)
        };

        // 3곳 중 하나라도 레이캐스트가 뚫리면(장애물에 안 맞으면) 즉시 true 반환 (Short-circuit 평가)
        foreach (Vector3 targetPoint in targetPoints)
        {
            Vector3 dir = (targetPoint - startPos).normalized;
            float actualDist = Vector3.Distance(startPos, targetPoint);

            // 장애물에 부딪히지 '않았다면' (!Physics.Raycast)
            if (!Physics.Raycast(startPos, dir, actualDist, _obstacleMask))
            {
                return true; // 하나라도 보였으니 연산 즉시 종료 (최적화)
            }
        }

        // 3곳 모두 벽이나 계단 모서리에 가려졌다면 안 보임
        return false;
    }
}