using UnityEngine;

/// <summary>
/// 몬스터가 플레이어를 시야에 포착하고 식별(경계도 상승)하는 상태입니다.
/// 경계도가 100%가 되면 추격(Chase) 상태로 돌입합니다.
/// </summary>
public class DetectState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    // 본 상태에서는 전역으로 유지할 캐싱 변수가 필요하지 않아 생략합니다.


    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public DetectState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();

        // 1. 관성으로 미끄러지지 않도록 제자리에 완벽히 정지
        owner.navAgent.isStopped = true;
        owner.navAgent.velocity = Vector3.zero;

        // 2. 자동 회전을 끄고 타겟을 향해 수동으로 부드럽게 회전하도록 세팅
        owner.navAgent.updateRotation = false;

        // [TODO] 경계/발견 애니메이션 실행 (예: owner.animHandler.SetSearching(true) 등)
    }

    public override void Exit()
    {
        base.Exit();

        // 상태를 벗어날 때 다시 에이전트의 길찾기 자동 회전을 켜줍니다.
        owner.navAgent.updateRotation = true;
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 부드러운 유도 회전과 경계도(Alertness) 게이지 계산을 처리합니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        if (!owner.IsServer) return;

        Transform target = owner.scanner.CurrentTarget;

        if (target != null)
        {
            // 1. 타겟이 보일 때: 경계도 서서히 상승
            owner.ServerAlertness += Time.deltaTime * data.alertnessIncreaseRate;

            // 2. 타겟 방향으로 부드럽게 회전 (시각적 처리)
            LookAtTarget(target);

            // 3. 경계도가 꽉 차면 맹렬한 추격(Chase) 시작
            if (owner.ServerAlertness >= 1.0f)
            {
                owner.ChangeState(MonsterStateType.Chase);
            }
        }
        else
        {
            // 4. 타겟이 장애물에 가려지거나 도망치면 경계도 서서히 감소
            owner.ServerAlertness -= Time.deltaTime * data.alertnessDecreaseRate;

            // 경계도가 0이 되어 완전히 까먹으면 다시 순찰(Patrol) 모드로 복귀
            if (owner.ServerAlertness <= 0f)
            {
                owner.ChangeState(MonsterStateType.Patrol);
            }
        }
    }

    /// <summary>
    /// 0.2초마다 실행: 주변 환경(시야/소리) 스캔을 업데이트합니다.
    /// </summary>
    protected override void OnTick()
    {
        // 시야/청각 시스템을 가동하여 새로운 타겟이나 변동 사항을 갱신합니다.
        owner.scanner.Tick();
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 타겟 방향으로 부드럽게 고개(몸통)를 돌립니다.
    /// </summary>
    private void LookAtTarget(Transform target)
    {
        Vector3 dir = (target.position - owner.transform.position).normalized;
        dir.y = 0; // 수직(위아래) 회전 방지

        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);

            // Slerp를 이용하여 지정된 속도(5f)로 부드럽게 목표 각도까지 회전합니다.
            owner.transform.rotation = Quaternion.Slerp(
                owner.transform.rotation,
                targetRot,
                Time.deltaTime * 5f
            );
        }
    }
}