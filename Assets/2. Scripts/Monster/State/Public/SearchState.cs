using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 몬스터가 플레이어를 놓쳤을 때, 마지막 이동 방향과 속도를 토대로 미래 위치를 예측하여 수색하는 상태입니다.
/// </summary>
public class SearchState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private float _totalSearchTimer;         // 전체 수색 지속 시간 (이 시간이 지나면 포기)
    private bool _isInvestigating;           // 현재 지점에 도착해서 두리번거리고 있는지 여부
    private float _pauseTimer;               // 조사 지점 도착 후 대기 타이머
    private int _searchAttemptCount;         // 랜덤 수색 구역 확장을 위한 시도 횟수 카운트

    private Vector3 _predictedPosition;      // 플레이어 이동 방향을 토대로 계산된 '도주 예상 지점'
    private List<Transform> _nearbyWaypoints = new List<Transform>(5); // 예상 지점 근처의 수색 후보지들
    private int _currentWaypointIndex = 0;   // 현재 몇 번째 거점을 수색 중인지 가리키는 인덱스


    // =========================================================
    // 2. 초기화 함수
    // =========================================================

    public SearchState(MonsterController owner) : base(owner)
    {
        // 수색 상태는 평문적인 순찰보다는 약간 빠른 판단이 필요할 수 있으나 기본 틱(0.2초)을 사용
    }

    public override void Enter()
    {
        base.Enter();

        // 0. 예외 처리: 마지막 목격 위치가 없으면 수색을 할 수 없으므로 즉시 순찰로 복귀
        if (owner.scanner.LastSeenPosition == Vector3.zero)
        {
            Debug.LogWarning($"<color=orange>[Search]</color> {owner.name}의 LastSeenPosition이 없어 순찰 상태로 복귀합니다.");
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 1. 수색 데이터 및 타이머 초기화
        _totalSearchTimer = 0f;
        _isInvestigating = false;
        _pauseTimer = 0f;
        _searchAttemptCount = 0;
        _currentWaypointIndex = 0;

        // 2. 에이전트 및 애니메이션 세팅 
        owner.navAgent.speed = data.patrolSpeed;
        owner.navAgent.isStopped = false;
        owner.animHandler.SetSearching(false);

        // 3. 지능적 예측 로직 실행
        CalculatePredictedPosition(); // 플레이어가 도망간 예상 지점 계산
        FindNearbySearchNodes();      // 그 예상 지점 근처의 주요 거점(Waypoint) 3곳 추출

        // 4. 첫 번째 목적지(예측 지점)로 이동 시작
        MoveToPosition(_predictedPosition);
        Debug.Log($"<color=magenta>[Search]</color> 도주 예측 지점 {_predictedPosition}으로 이동하여 수색을 시작합니다.");
    }

    public override void Exit()
    {
        base.Exit();

        owner.animHandler.SetSearching(false);

        // 네비게이션이 멈춰있었다면 다시 풀어줌
        if (owner.navAgent != null && owner.navAgent.isOnNavMesh)
        {
            owner.navAgent.isStopped = false;
        }

        _isInvestigating = false;
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 목적지 도착 확인, 두리번거림 대기, 수색 포기 타이머를 계산합니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        if (owner.CurrentStateNet.Value != MonsterStateType.Search) return;

        // 1. 도착 확인 로직 (경로 계산 중이 아니고, 목적지에 거의 다다랐을 때)
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 0.1f)
        {
            if (!_isInvestigating)
            {
                StartInvestigating(); // 방금 도착했다면 두리번거리기 시작
            }
            else
            {
                HandleInvestigation(); // 이미 두리번거리고 있다면 타이머 계산
            }
        }

        // 2. 전체 수색 제한 시간(포기 시간) 계산
        _totalSearchTimer += Time.deltaTime;

        if (_totalSearchTimer >= data.maxSearchDuration)
        {
            Debug.Log($"<color=magenta>[Search]</color> 수색 시간을 초과하여 타겟을 포기하고 순찰로 복귀합니다.");
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }
    }

    /// <summary>
    /// 0.2초마다 실행: 시야/청각으로 타겟을 찾았는지, 앞에 문이 있는지를 확인합니다.
    /// </summary>
    protected override void OnTick()
    {
        owner.scanner.Tick();

        // 1. 수색 중 플레이어의 꼬리를 다시 잡았다면 즉시 감지(Detect) 또는 추격 상태로 전환
        if (owner.scanner.CurrentTarget != null)
        {
            owner.ChangeState(MonsterStateType.Detect);
            return;
        }

        // 2. 이동 중 닫힌 문이 앞을 가로막고 있다면 문 열기 시도
        if (owner.CheckAndHandleDoor()) return;
    }


    // =========================================================
    // 4. 퍼블릭 함수
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수
    // =========================================================

    /// <summary>
    /// 플레이어의 마지막 이동 속도(관성)와 방향을 이용해 미래 도주 위치를 예측합니다.
    /// </summary>
    private void CalculatePredictedPosition()
    {
        // 마지막 목격 위치 + (마지막 이동 속도 * 예측 가중치 시간)
        Vector3 rawPrediction = owner.scanner.LastSeenPosition + (owner.scanner.LastTargetVelocity * data.predictionTime);

        // 예측 지점이 벽 너머나 허공이 아닌지(NavMesh 위인지) 검증
        if (NavMesh.SamplePosition(rawPrediction, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            _predictedPosition = hit.position;
        }
        else
        {
            // 예측 지점이 맵 밖이라면 안전하게 마지막 목격 위치를 기본값으로 사용
            _predictedPosition = owner.scanner.LastSeenPosition;
        }
    }

    /// <summary>
    /// 예측 지점 주변의 Waypoint들을 탐색하여 가장 가까운 3곳의 효율적인 수색 경로를 생성합니다.
    /// </summary>
    private void FindNearbySearchNodes()
    {
        _nearbyWaypoints.Clear();

        if (owner.waypointManager == null || owner.waypointManager.waypoints == null) return;

        float radiusSqr = data.searchNodeRadius * data.searchNodeRadius;
        var allWaypoints = owner.waypointManager.waypoints;

        // 모든 거점을 순회하며 반경 내에 있는지 확인
        for (int i = 0; i < allWaypoints.Count; i++)
        {
            Transform wp = allWaypoints[i];
            float distSqr = (wp.position - _predictedPosition).sqrMagnitude;

            // 반경 내에 있다면, 거리가 가까운 순서대로 삽입 정렬(Insertion Sort)
            if (distSqr <= radiusSqr)
            {
                int insertIndex = 0;
                for (; insertIndex < _nearbyWaypoints.Count; insertIndex++)
                {
                    float existingDistSqr = (_nearbyWaypoints[insertIndex].position - _predictedPosition).sqrMagnitude;
                    if (distSqr < existingDistSqr)
                    {
                        break; // 기존 거점보다 내가 더 가깝다면 이 자리에 새치기 삽입
                    }
                }

                // 상위 3개까지만 리스트에 담아둠
                if (insertIndex < 3)
                {
                    _nearbyWaypoints.Insert(insertIndex, wp);
                    if (_nearbyWaypoints.Count > 3)
                    {
                        _nearbyWaypoints.RemoveAt(3); // 4개가 되면 가장 먼 녀석을 버림
                    }
                }
            }
        }
    }

    /// <summary>
    /// 수색 지점에 도착했을 때 제자리에 정지하여 두리번거리는 연출을 시작합니다.
    /// </summary>
    private void StartInvestigating()
    {
        _isInvestigating = true;
        _pauseTimer = 0f;

        // 미끄러짐 방지를 위해 속도 완벽 차단
        owner.navAgent.isStopped = true;
        owner.navAgent.velocity = Vector3.zero;

        owner.animHandler.SetSearching(true);
    }

    /// <summary>
    /// 지정된 시간 동안 두리번거리기를 마친 후 다음 지점으로 이동을 지시합니다.
    /// </summary>
    private void HandleInvestigation()
    {
        _pauseTimer += Time.deltaTime;

        if (_pauseTimer >= data.searchPauseDuration)
        {
            _isInvestigating = false;
            owner.animHandler.SetSearching(false);

            MoveToNextSearchPoint();
        }
    }

    /// <summary>
    /// 다음 수색 지점(미리 찾아둔 거점 또는 랜덤 확장 구역)을 결정하여 이동합니다.
    /// </summary>
    private void MoveToNextSearchPoint()
    {
        // 1. 미리 찾아둔 주변 주요 거점(Waypoint 상위 3개)이 남아있다면 순차적으로 방문
        if (_currentWaypointIndex < _nearbyWaypoints.Count)
        {
            MoveToPosition(_nearbyWaypoints[_currentWaypointIndex].position);
            _currentWaypointIndex++;
            Debug.Log($"<color=magenta>[Search]</color> 주요 거점 수색 중: {_currentWaypointIndex}/{_nearbyWaypoints.Count}");
        }
        else
        {
            // 2. 주요 거점을 다 돌았는데도 못 찾았다면, 점진적으로 반경을 넓히며 랜덤 수색
            InvestigateRandomNearby();
        }
    }

    /// <summary>
    /// 특정 위치 주변을 랜덤하게 찍어 수색합니다. (시도할수록 범위가 넓어짐)
    /// </summary>
    private void InvestigateRandomNearby()
    {
        _searchAttemptCount++;

        // 시도 횟수가 늘어날수록 수색 반경을 2m씩 확장시킴 (기본 5m)
        float currentRadius = 5f + (_searchAttemptCount * 2f);
        Vector3 randomDir = Random.insideUnitSphere * currentRadius;
        randomDir += owner.transform.position; // 현재 몬스터 위치 기준으로 주변 탐색

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, currentRadius, NavMesh.AllAreas))
        {
            MoveToPosition(hit.position);
            Debug.Log($"<color=magenta>[Search]</color> 랜덤 수색 구역 확장: {currentRadius}m 반경 탐색");
        }
    }

    /// <summary>
    /// 에이전트를 깨우고 목표 위치로 출발시키는 공통 헬퍼 함수입니다.
    /// </summary>
    private void MoveToPosition(Vector3 pos)
    {
        owner.navAgent.isStopped = false;
        owner.navAgent.SetDestination(pos);
    }
}