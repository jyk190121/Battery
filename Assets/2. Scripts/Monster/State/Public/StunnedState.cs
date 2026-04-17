using UnityEngine;

/// <summary>
/// 섬광탄이나 특수 기믹에 의해 몬스터가 기절(스턴)했을 때 진입하는 상태입니다.
/// 일정 시간 동안 모든 이동과 AI 판단을 멈추고 대기합니다.
/// </summary>
public class StunnedState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private float _stunTimer;

    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public StunnedState(MonsterController owner) : base(owner)
    {
        // 기절한 상태에서는 AI가 사고할 필요가 없으므로 틱 주기를 무한에 가깝게 늘려 CPU를 최적화
        this.currentTickInterval = 999f;
    }

    public override void Enter()
    {
        base.Enter();
        _stunTimer = 0f;

        Debug.Log($"<color=cyan>[StunnedState]</color> {owner.gameObject.name} 스턴 시작! (목표 시간: {owner.CurrentStunDuration}초)");

        // 1. 발 묶기 (완전 정지 및 미끄러짐 방지)
        StopMovement();

        // 2. 어그로/타겟 초기화 (눈뽕/기절로 인해 쫓던 대상을 잃어버림)
        ClearAggro();

        // 3. 스턴 애니메이션 재생
        PlayStunAnimation();
    }

    public override void Exit()
    {
        base.Exit();

        // 스턴이 끝날 때 애니메이션 락을 풀어주거나 하는 추가 작업이 필요하면 여기에 작성합니다.
        // [TODO] if (owner.animHandler != null) owner.animHandler.StopStun(); 

        Debug.Log($"<color=cyan>[StunnedState]</color> {owner.gameObject.name} 스턴 종료! 수색 상태로 넘어갑니다.");
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 기절해 있는 시간을 계산하고 상태를 해제합니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        _stunTimer += Time.deltaTime;

        // 지정된 스턴 시간이 다 끝나면?
        if (_stunTimer >= owner.CurrentStunDuration)
        {
            // 기절에서 깨어나 두리번거리며 타겟을 다시 찾는 상태(Search)로 복귀
            owner.ChangeState(MonsterStateType.Search);
        }
    }

    /// <summary>
    /// 기절한 상태이므로 AI 두뇌(감각 스캔 등)를 완전히 정지시킵니다. 
    /// </summary>
    protected override void OnTick()
    {
        // 의도적으로 비워둠
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 에이전트의 이동 경로를 초기화하고 관성에 의한 미끄러짐을 방지합니다.
    /// </summary>
    private void StopMovement()
    {
        if (owner.navAgent != null && owner.navAgent.isActiveAndEnabled && owner.navAgent.isOnNavMesh)
        {
            owner.navAgent.isStopped = true;
            owner.navAgent.ResetPath();
            owner.navAgent.velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 스캐너의 타겟을 비워버려, 스턴이 풀린 후 플레이어를 처음부터 다시 찾게 만듭니다.
    /// </summary>
    private void ClearAggro()
    {
        if (owner.scanner != null)
        {
            owner.scanner.SetForceTarget(null);
        }
    }

    /// <summary>
    /// 기절 애니메이션을 재생합니다.
    /// </summary>
    private void PlayStunAnimation()
    {
        // 추후 애니메이터 세팅이 완료되면 주석을 해제하고 사용하세요!
        // if (owner.animHandler != null)
        // {
        //     owner.animHandler.PlayStun();
        // }
    }
}