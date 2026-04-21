using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 인형(Doll)이 플레이어의 등 뒤를 집요하게 쫓아다니는 상태입니다.
/// </summary>
public class StalkState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private Transform _targetPlayer;
    private PlayerController _playerController;

    // 텔레포트 직후 억울하게 눈이 마주쳤다고 판정되는 것을 막기 위한 유예 시간 타이머
    private float _graceTimer = 0f;


    // =========================================================
    // 2. 초기화 함수
    // =========================================================

    public StalkState(MonsterController owner) : base(owner)
    {
        // 상태 생성 시 필요한 로직 (현재는 비워둠)
    }

    public override void Enter()
    {
        base.Enter();

        _targetPlayer = owner.scanner.CurrentTarget;
        if (_targetPlayer == null) return;

        _playerController = _targetPlayer.GetComponent<PlayerController>();

        // 플레이어 등 뒤 위치 계산 
        Vector3 backPos = _targetPlayer.position - (_targetPlayer.forward * owner.monsterData.dollNormalDistance);
        Vector3 finalPos = GetNavMeshPosition(backPos);

        // 에이전트를 끄지 말고 Warp를 사용 
        owner.navAgent.Warp(finalPos);
        owner.navAgent.speed = owner.monsterData.chaseSpeed;

        // 인형이 항상 플레이어를 쳐다보도록 NavMeshAgent의 자동 회전 끄기
        owner.navAgent.updateRotation = false;

        // 진입 직후 유예 시간 설정 (잔상 버그 방지)
        _graceTimer = owner.monsterData.dollGracePeriod;
    }

    public override void Exit()
    {
        base.Exit();

        // 상태를 벗어날 때 NavMeshAgent의 설정을 원상복구 합니다.
        if (owner.navAgent != null)
        {
            owner.navAgent.updateRotation = true;
        }
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 추적, 회전, 발각 판정을 수행합니다.
    /// </summary>
    public override void Update()
    {
        base.Update();
        if (_targetPlayer == null || _playerController == null) return;

        // 1. 타겟 유효성 검사 (죽었거나 건물을 나갔는지)
        if (CheckTargetInvalid()) return;

        // 2. 등 뒤로 쫓아가는 이동 및 시선 고정 연산
        HandleMovementAndRotation();

        // 3. 접촉 및 눈 마주침(Scream) 판정
        CheckScreamTriggers();
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 타겟이 사망했거나 건물 밖으로 도망쳤는지 확인하고, 추적을 포기합니다.
    /// </summary>
    private bool CheckTargetInvalid()
    {
        if (_playerController.isDead.Value || !_playerController.isInsideFacility.Value)
        {
            Debug.Log("<color=green>[Doll]</color> 타겟이 죽었거나 건물 밖으로 나갔습니다. 추적을 포기합니다.");
            owner.ChangeState(MonsterStateType.Patrol);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 플레이어의 등 뒤 목표 지점을 찾아 이동하고, 항상 플레이어를 응시하게 만듭니다.
    /// </summary>
    private void HandleMovementAndRotation()
    {
        // 거리 연산 
        Vector2 playerPos2D = new Vector2(_targetPlayer.position.x, _targetPlayer.position.z);
        Vector2 dollPos2D = new Vector2(owner.transform.position.x, owner.transform.position.z);
        float currentDist2D = Vector2.Distance(playerPos2D, dollPos2D);

        float yDifference = Mathf.Abs(_targetPlayer.position.y - owner.transform.position.y);
        float targetDistance = (yDifference > 0.5f) ? owner.monsterData.dollStairDistance : owner.monsterData.dollNormalDistance;

        // 플레이어 -> 인형 방향 (Y축 무시)
        Vector3 dirFromPlayerToDoll = (owner.transform.position - _targetPlayer.position);
        dirFromPlayerToDoll.y = 0f;
        Vector3 flatPlayerForward = _targetPlayer.forward;
        flatPlayerForward.y = 0f;

        // 이동 로직 (플레이어가 뒷걸음질 치면 뒤로 빼지 않고 대기)
        float behindDot = Vector3.Dot(flatPlayerForward.normalized, dirFromPlayerToDoll.normalized);
        if (behindDot < -0.5f && currentDist2D <= targetDistance + 0.1f)
        {
            owner.navAgent.SetDestination(owner.transform.position);
        }
        else
        {
            Vector3 targetBackPos = _targetPlayer.position - (_targetPlayer.forward * targetDistance);
            owner.navAgent.SetDestination(targetBackPos);
        }

        // 시선 처리: 이동 중에도 항상 플레이어를 쳐다보게 수동 회전
        Vector3 dirToPlayer = (_targetPlayer.position - owner.transform.position).normalized;
        dirToPlayer.y = 0f;

        if (dirToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
            owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    /// <summary>
    /// 인형과 부딪히거나 눈을 마주쳤는지 검사하고 게임 오버 기믹을 발동합니다.
    /// </summary>
    private void CheckScreamTriggers()
    {
        // 유예 시간(0.5초)이 남아있다면 검사 무시
        if (_graceTimer > 0f)
        {
            _graceTimer -= Time.deltaTime;
            return;
        }

        // 조건 A: 접촉 (뒷걸음질 치다가 인형과 부딪힘)
        Vector2 playerPos2D = new Vector2(_targetPlayer.position.x, _targetPlayer.position.z);
        Vector2 dollPos2D = new Vector2(owner.transform.position.x, owner.transform.position.z);

        if (Vector2.Distance(playerPos2D, dollPos2D) <= owner.monsterData.dollBumpDistance)
        {
            TriggerScream("벽에 막혔거나 뒷걸음질 쳐서 인형과 부딪혔습니다!");
            return;
        }

        // 조건 B: 시선 (몸통을 돌려 인형을 쳐다봄)
        Vector3 playerHeadPos = _targetPlayer.position + (Vector3.up * 1.5f);
        Vector3 dirToDoll = (owner.transform.position - playerHeadPos).normalized;
        dirToDoll.y = 0f;

        Vector3 flatPlayerForward = _targetPlayer.forward;
        flatPlayerForward.y = 0f;

        float lookDot = Vector3.Dot(flatPlayerForward.normalized, dirToDoll.normalized);

        // 플레이어의 몸이 인형 쪽을 향하게 되면 발각!
        if (lookDot > owner.monsterData.dollCatchDotThreshold)
        {
            TriggerScream("뒤를 돌아봐서 인형과 마주쳤습니다!");
            return;
        }
    }

    /// <summary>
    /// 공포 연출(Scream) 상태로 넘깁니다.
    /// </summary>
    private void TriggerScream(string reason)
    {
        Debug.Log($"<color=red>[Doll Trigger]</color> {reason}");
        owner.ChangeState(MonsterStateType.Scream);
    }

    /// <summary>
    /// 강제 이동 시 Y축 버그를 막기 위해 NavMesh의 가장 가까운 바닥 좌표를 반환합니다.
    /// </summary>
    private Vector3 GetNavMeshPosition(Vector3 rawPos)
    {
        if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return rawPos;
    }
}