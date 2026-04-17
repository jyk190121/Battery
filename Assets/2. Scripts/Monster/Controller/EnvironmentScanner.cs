using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 감각(시각, 청각) 및 타겟 추적을 담당하는 핵심 AI 스캐너 클래스입니다.
/// </summary>
public class EnvironmentScanner : MonoBehaviour
{
    // =========================================================
    // 1. 변수 선언부 (Variables)
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

    // [프라이빗 변수] 내부 연산 및 최적화 캐싱용 변수들 (_ 접두사 사용)
    private Vector3 _previousTargetPos;
    private NavMeshPath _path;
    private float _viewRangeSqr;
    private float _timeLastSeen = 0f;

    // 길찾기 연산(CPU 폭탄) 캐싱용 딕셔너리
    private Dictionary<Transform, float> _lastPathCheckTimes = new Dictionary<Transform, float>();
    private Dictionary<Transform, bool> _cachedPathResults = new Dictionary<Transform, bool>();
    private float _pathCheckInterval = 0.5f; // 0.5초마다만 무거운 길찾기 연산 수행


    // =========================================================
    // 2. 초기화 함수 (Init)
    // =========================================================

    /// <summary>
    /// 몬스터가 스폰될 때 컨트롤러에 의해 초기화됩니다.
    /// </summary>
    public void Init(MonsterController controller, MonsterData monsterData)
    {
        owner = controller;
        data = monsterData;
        _path = new NavMeshPath();

        // 연산 최적화를 위해 거리의 제곱값을 미리 계산해 둡니다.
        _viewRangeSqr = data.viewRange * data.viewRange;
    }


    // =========================================================
    // 3. 유니티 루프 및 콜백 (OnDrawGizmos 등)
    // =========================================================

    private void OnDrawGizmos()
    {
        if (data == null) return;

        // 시각 및 청각 범위 시각화
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.viewRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.hearingRange);

        // 마지막 목격 위치 시각화 (기억 중이면 주황색, 잊혀지면 노란색)
        if (LastSeenPosition != Vector3.zero)
        {
            Gizmos.color = (Time.time - _timeLastSeen <= data.visionMemoryTime) ? new Color(1f, 0.5f, 0f) : Color.yellow;
            Gizmos.DrawSphere(LastSeenPosition, 0.5f);
            Gizmos.DrawLine(transform.position, LastSeenPosition);
        }
    }


    // =========================================================
    // 4. 퍼블릭 함수 (Public Methods : 외부에서 부르는 창구)
    // =========================================================

    /// <summary>
    /// 컨트롤러의 OnTick(주기적 AI 연산)에서 호출되어 시야 감지 연산을 수행합니다.
    /// </summary>
    public void Tick()
    {
        if (!owner.IsServer) return;

        Transform bestTarget = null;
        float minSqrDistance = float.MaxValue;
        float targetStickiness = 2.0f; // 현재 타겟은 2m 더 멀리 있어도 유지함 (어그로 핑퐁 방지)

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

            // 시야 반경 밖이면 무시
            if (currentSqrDist > _viewRangeSqr) continue;

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
                // [최적화 적용] 0.5초 캐싱된 길찾기 가능 여부 확인
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

        // 거리가 청각 범위 * 소리 크기 이내라면 감지 성공
        if (distance <= data.hearingRange * noiseLevel)
        {
            LastHeardPosition = soundOrigin;
            Debug.Log($"[{owner.name}] 소리 감지: {soundOrigin}");

            // 순찰 또는 정지 상태일 때 소리가 나면 즉시 해당 위치로 수색 모드 진입
            if (owner.CurrentStateNet.Value == MonsterStateType.Patrol ||
                owner.CurrentStateNet.Value == MonsterStateType.Idle)
            {
                LastSeenPosition = soundOrigin;
                owner.ChangeState(MonsterStateType.Search);
            }
        }
    }

    /// <summary>
    /// 외부 상태(CeilingWait 등)에서 특정 타겟을 강제로 고정할 때 사용합니다.
    /// </summary>
    public void SetForceTarget(Transform newTarget)
    {
        UpdateTargetData(newTarget);
        string targetName = newTarget != null ? newTarget.name : "None (초기화됨)";
        Debug.Log($"[{owner.name}] 타겟 강제 고정: {targetName}");
    }


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 (Private Methods : 내부 연산용)
    // =========================================================

    /// <summary>
    /// 타겟의 상태에 따라 위치와 예측 속도를 업데이트합니다.
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
            if (_previousTargetPos != Vector3.zero)
            {
                float dt = Time.deltaTime;
                if (dt > 0) // 0으로 나누기 방지
                {
                    LastTargetVelocity = (currentPos - _previousTargetPos) / dt;
                }
            }
            _previousTargetPos = currentPos;
        }
        else
        {
            // 타겟을 놓친 경우 이전 위치 데이터 초기화 
            _previousTargetPos = Vector3.zero;
        }
    }

    /// <summary>
    /// 타겟이 안전 구역에 있는지 검사합니다.
    /// </summary>
    private bool IsTargetValid(GameObject target) => !owner.IsInSafeZone(target);

    /// <summary>
    /// NavMesh를 이용해 타겟까지의 실제 보행 거리가 시야 범위 내인지 확인합니다.
    /// CPU 부하를 막기 위해 타겟당 0.5초에 한 번만 연산하고 결과를 캐싱합니다.
    /// </summary>
    private bool IsPathReasonable(Transform target)
    {
        // 1. 이미 최근 0.5초 안에 계산한 적이 있는지 확인 (캐시 히트)
        if (_lastPathCheckTimes.TryGetValue(target, out float lastCheckTime))
        {
            if (Time.time - lastCheckTime < _pathCheckInterval)
            {
                return _cachedPathResults[target]; // 무거운 연산 없이 기존 결과 즉시 반환
            }
        }

        // 2. 0.5초가 지났거나 처음 본 타겟이라면 무거운 길찾기 계산 수행 (캐시 미스)
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

        // 3. 연산 결과를 딕셔너리에 최신화
        _lastPathCheckTimes[target] = Time.time;
        _cachedPathResults[target] = isValid;

        return isValid;
    }

    /// <summary>
    /// 레이캐스트를 이용한 시야 가림(벽 등 장애물) 여부를 확인합니다.
    /// </summary>
    private bool HasLineOfSight(Transform target)
    {
        Vector3 startPos = transform.position + (Vector3.up * 1.5f); // 몬스터 눈높이
        Vector3 targetPos = target.position + (Vector3.up * 1.0f);   // 플레이어 가슴 높이

        Vector3 dir = (targetPos - startPos).normalized;
        float actualDist = Vector3.Distance(startPos, targetPos);

        // 장애물 레이어에 부딪히면 시야가 차단된 것(!Raycast)
        return !Physics.Raycast(startPos, dir, actualDist, _obstacleMask);
    }
}