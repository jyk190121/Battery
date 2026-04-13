using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SearchState: 플레이어를 놓쳤을 때 마지막 위치와 이동 방향을 토대로 수색하는 상태
/// </summary>
public class SearchState : MonsterBaseState
{
    private float totalSearchTimer;             // 전체 수색 지속 시간 제한용
    private bool isInvestigating;               // 현재 지점을 조사(두리번) 중인지 여부
    private float pauseTimer;                   // 조사 지점 도착 후 대기 타이머
    private int searchAttemptCount;             // 랜덤 수색 시도 횟수

    private Vector3 predictedPosition;          // 플레이어 이동 방향을 토대로 계산된 예측 지점
    private List<Transform> nearbyWaypoints = new List<Transform>(); // 주변 수색 후보지들
    private int currentWaypointIndex = 0;       // 현재 몇 번째 후보지를 수색 중인지

    public SearchState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();

        // 마지막 목격 위치 정보가 없으면 수색이 불가능하므로 즉시 순찰로 복귀
        if (owner.scanner.LastSeenPosition == Vector3.zero)
        {
            Debug.LogWarning($"[{owner.name}] LastSeenPosition이 없어 순찰 상태로 복귀합니다.");
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 1. 수색 데이터 초기화
        totalSearchTimer = 0f;
        isInvestigating = false;
        pauseTimer = 0f;
        searchAttemptCount = 0;
        currentWaypointIndex = 0;

        // 2. 에이전트 및 애니메이션 기본 설정
        owner.navAgent.speed = data.patrolSpeed; // 수색 시에는 순찰 속도로 이동
        owner.navAgent.isStopped = false;
        owner.animHandler.SetSearching(false);   // 이동 중에는 수색 애니메이션 끔
        owner.animHandler.SetSpeed(data.patrolSpeed);          // 걷기 애니메이션 활성화

        // 3. 지능적 예측 로직 실행
        CalculatePredictedPosition(); // 플레이어가 도망간 예상 지점 계산
        FindNearbySearchNodes();      // 그 지점 근처의 주요 거점(Waypoint) 찾기

        // 4. 첫 번째 목적지(예측 지점)로 이동 시작
        MoveToPosition(predictedPosition);
        Debug.Log($"[Search] 예측 지점 {predictedPosition}으로 이동하여 수색을 시작합니다.");
    }

    /// <summary>
    /// 플레이어의 마지막 이동 속도와 방향을 이용해 미래 위치를 예측 (Extrapolation)
    /// </summary>
    private void CalculatePredictedPosition()
    {
        // 마지막 위치 + (마지막 이동 벡터 * 예측 가중치 시간)
        Vector3 rawPrediction = owner.scanner.LastSeenPosition + (owner.scanner.LastTargetVelocity * data.predictionTime);

        // 예측 지점이 NavMesh(걸을 수 있는 곳)인지 검증 및 보정 (벽 너머로 가는 것 방지)
        if (NavMesh.SamplePosition(rawPrediction, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            predictedPosition = hit.position;
        }
        else
        {
            // 예측 지점이 부적절하면 마지막 목격 위치를 기본값으로 사용
            predictedPosition = owner.scanner.LastSeenPosition;
        }
    }

    /// <summary>
    /// 예측 지점 주변의 Waypoint들을 탐색하여 효율적인 수색 경로 생성
    /// </summary>
    private void FindNearbySearchNodes()
    {
        nearbyWaypoints.Clear();
        if (owner.waypointManager == null) return;

        // 맵의 전체 Waypoint 중 예측 지점 반경(searchNodeRadius) 내에 있는 것들을 거리순으로 정렬하여 선택
        nearbyWaypoints = owner.waypointManager.waypoints
            .Where(w => Vector3.Distance(w.position, predictedPosition) <= data.searchNodeRadius)
            .OrderBy(w => Vector3.Distance(w.position, predictedPosition))
            .Take(3) // 너무 많으면 비효율적이므로 상위 3개만 선정
            .ToList();
    }

    protected override void OnTick()
    {
        // 수색 중에도 감각 시스템 가동
        owner.scanner.Tick();

        // 1. 수색 중 플레이어를 다시 발견하면 즉시 감지/추격 상태로 전환
        if (owner.scanner.CurrentTarget != null)
        {
            owner.ChangeState(MonsterStateType.Detect);
            return;
        }

        // 2. 문 상호작용 체크 (수색 경로 상에 문이 있으면 열기)
        owner.CheckAndHandleDoor();

        // 3. 전체 수색 시간 초과 시 포기하고 순찰로 복귀
        totalSearchTimer += currentTickInterval;
        if (totalSearchTimer >= data.maxSearchDuration)
        {
            Debug.Log($"[Search] 수색 시간 초과로 순찰로 복귀합니다.");
            owner.ChangeState(MonsterStateType.Patrol);
        }
    }

    public override void Update()
    {
        base.Update();

        // [도착 확인 로직]
        // 경로 계산 중이 아니고, 목적지까지의 거리가 정지 거리 이내일 때
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 0.1f)
        {
            if (!isInvestigating)
            {
                StartInvestigating(); // 도착했으므로 조사 시작
            }
            else
            {
                HandleInvestigation(); // 조사 중 타이머 체크
            }
        }
    }

    /// <summary>
    /// 조사 지점에 도착했을 때 정지하고 두리번거리는 상태로 전환
    /// </summary>
    private void StartInvestigating()
    {
        isInvestigating = true;
        pauseTimer = 0f;
        owner.navAgent.isStopped = true;  // 제자리에 멈춤
        //owner.animHandler.SetSpeed(0f);   // 이동 애니메이션 중지
        owner.animHandler.SetSearching(true); // 두리번거리는 애니메이션 실행
    }

    /// <summary>
    /// 설정된 시간 동안 조사를 마친 후 다음 지점으로 이동 여부 결정
    /// </summary>
    private void HandleInvestigation()
    {
        pauseTimer += Time.deltaTime;
        if (pauseTimer >= data.searchPauseDuration)
        {
            isInvestigating = false;
            owner.animHandler.SetSearching(false);
            MoveToNextSearchPoint();
        }
    }

    /// <summary>
    /// 다음 수색 지점(Waypoint 또는 랜덤)을 결정하여 이동 명령 전달
    /// </summary>
    private void MoveToNextSearchPoint()
    {
        // 1. 미리 찾아둔 주변 주요 거점(Waypoint)이 남아있다면 순차 수색
        if (currentWaypointIndex < nearbyWaypoints.Count)
        {
            MoveToPosition(nearbyWaypoints[currentWaypointIndex].position);
            currentWaypointIndex++;
            Debug.Log($"[Search] 주요 거점 수색 중: {currentWaypointIndex}/{nearbyWaypoints.Count}");
        }
        else
        {
            // 2. 주요 거점을 다 돌았다면 주변 영역을 점진적으로 넓히며 랜덤 수색
            InvestigateRandomNearby();
        }
    }

    /// <summary>
    /// 공용 이동 함수: 에이전트 속도 설정 및 애니메이션 동기화
    /// </summary>
    private void MoveToPosition(Vector3 pos)
    {
        owner.navAgent.isStopped = false;
        owner.navAgent.SetDestination(pos);
        owner.animHandler.SetSpeed(data.patrolSpeed);
    }

    /// <summary>
    /// 특정 위치 주변을 랜덤하게 찍어 수색 (NavMesh 기반)
    /// </summary>
    private void InvestigateRandomNearby()
    {
        searchAttemptCount++;
        // 시도 횟수가 늘어날수록 수색 범위를 점점 넓힘
        float currentRadius = 5f + (searchAttemptCount * 2f);
        Vector3 randomDir = Random.insideUnitSphere * currentRadius;
        randomDir += owner.transform.position; // 현재 위치 기준으로 주변 탐색

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, currentRadius, NavMesh.AllAreas))
        {
            MoveToPosition(hit.position);
            Debug.Log($"[Search] 랜덤 수색 구역 확장: {currentRadius}m");
        }
    }
}