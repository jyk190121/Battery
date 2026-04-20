using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 감각(시각, 청각) 및 타겟 추적을 담당하는 핵심 AI 스캐너 클래스입니다.
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
        // 활성화될 때 매니저에 등록
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.RegisterScanner(this);
    }

    private void OnDisable()
    {
        // 비활성화(사망 등)될 때 매니저에서 제거
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.UnregisterScanner(this);
    }


    // =========================================================
    // 3. 유니티 루프 및 콜백 (OnDrawGizmos 등)
    // =========================================================

    private void OnDrawGizmos()
    {
        if (data == null) return;

        // 청각 범위 시각화 (노란색 원)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.hearingRange);

        // 시야 범위 및 각도(FOV) 시각화 (빨간색 부채꼴)
        Gizmos.color = Color.red;

        // 정면 기준 좌우 시야각 끝자락을 구함
        Vector3 rightViewDir = Quaternion.Euler(0, data.viewAngle * 0.5f, 0) * transform.forward;
        Vector3 leftViewDir = Quaternion.Euler(0, -data.viewAngle * 0.5f, 0) * transform.forward;

        // 씬 뷰에 시야각 라인을 그림 (이 선 밖으로 나가면 몬스터가 못 봅니다!)
        Gizmos.DrawRay(transform.position, rightViewDir * data.viewRange);
        Gizmos.DrawRay(transform.position, leftViewDir * data.viewRange);

        // 마지막 목격 위치 시각화
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

    /// <summary>
    /// 컨트롤러의 OnTick에서 호출되어 시야 감지 연산을 수행합니다.
    /// </summary>
    public void Tick()
    {
        if (!owner.IsServer) return;

        Transform bestTarget = null;
        float minSqrDistance = float.MaxValue;
        float targetStickiness = 2.0f; // 현재 타겟은 2m 더 멀리 있어도 유지함

        foreach (PlayerController player in PlayerController.AllPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.isDead.Value) continue;
            if (!IsTargetValid(player.gameObject)) continue;

            Vector3 diff = player.transform.position - transform.position;
            float currentSqrDist = diff.sqrMagnitude;

            // 현재 타겟이라면 거리 판정을 더 후하게 줌
            if (CurrentTarget != null && player.transform == CurrentTarget)
            {
                currentSqrDist -= (targetStickiness * targetStickiness);
            }

            // 1. 최대 시야 반경(viewRange)을 벗어나면 무시
            if (currentSqrDist > _viewRangeSqr) continue;

            // 2. 시야각(FOV) 및 기척(Proximity) 감지 시스템
            Vector3 dirToPlayer = diff.normalized;
            // 몬스터의 정면과 플레이어 사이의 각도를 구합니다. (0도면 정면, 180도면 완전 뒤)
            float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);

            // [기척 감지] 몬스터 등 뒤에 있더라도 2m(제곱값 4) 이내로 바짝 붙으면 각도 무시하고 눈치챔
            bool isCloseEnoughToFeel = currentSqrDist <= (2.0f * 2.0f);

            // 시야각의 절반(좌우)을 벗어났고, 바짝 붙어있는 것도 아니라면? -> 못 본 것으로 간주하고 패스!
            if (angleToPlayer > data.viewAngle * 0.5f && !isCloseEnoughToFeel)
            {
                continue;
            }
            // =================================================================

            // 3. 시야 가림(벽 등 장애물) 레이캐스트 검사
            bool hasLOS = HasLineOfSight(player.transform);

            // 시야에서 잠깐 사라져도 기억 시간 내라면 보인 것으로 간주
            if (!hasLOS && CurrentTarget != null && player.transform == CurrentTarget)
            {
                if (Time.time - _timeLastSeen <= data.visionMemoryTime)
                {
                    hasLOS = true;
                }
            }

            if (hasLOS)
            {
                // 0.5초 캐싱된 길찾기 가능 여부 확인
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

        // 새로운 타겟이거나 타겟을 유지 중일 때 목격 시간 갱신
        if (bestTarget != null && bestTarget != CurrentTarget)
        {
            _timeLastSeen = Time.time;
        }

        UpdateTargetData(bestTarget);
    }

    /// <summary>
    /// 외부 SoundManager 등에서 소리가 발생했을 때 호출하는 훅(Hook) 함수
    /// </summary>
    public void OnHeardSound(Vector3 soundOrigin, float noiseLevel)
    {
        float distance = Vector3.Distance(transform.position, soundOrigin);

        // 소리는 시야각(FOV)의 영향을 받지 않고 360도로 감지됩니다.
        if (distance <= data.hearingRange * noiseLevel)
        {
            LastHeardPosition = soundOrigin;
            Debug.Log($"<color=yellow>[소리 감지]</color> {owner.name}이(가) 소리를 들었습니다: {soundOrigin}");

            // 순찰, 정지, 혹은 수색 중일 때 소리가 나면 그곳으로 '조사(Investigate)'를 하러 갑니다.
            if (owner.CurrentStateNet.Value == MonsterStateType.Patrol ||
                owner.CurrentStateNet.Value == MonsterStateType.Idle ||
                owner.CurrentStateNet.Value == MonsterStateType.Search) // 수색 중 다른 소리가 나면 갱신
            {
                // 소리가 난 위치를 저장하고 조사 상태로 변경
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

    private bool IsTargetValid(GameObject target) => !owner.IsInSafeZone(target);

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

    private bool HasLineOfSight(Transform target)
    {
        Vector3 startPos = transform.position + (Vector3.up * 1.5f);
        Vector3 targetPos = target.position + (Vector3.up * 1.0f);

        Vector3 dir = (targetPos - startPos).normalized;
        float actualDist = Vector3.Distance(startPos, targetPos);

        return !Physics.Raycast(startPos, dir, actualDist, _obstacleMask);
    }
}