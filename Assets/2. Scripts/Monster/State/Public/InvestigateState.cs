using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터가 소리나 수상한 흔적을 감지했을 때 해당 지점까지 확인하러 가는 상태입니다.
/// </summary>
public class InvestigateState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private float _stuckTimer;
    private Vector3 _investigateTarget;


    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public InvestigateState(MonsterController owner) : base(owner)
    {
        // 주변을 살피며 가야 하므로 기본 틱(0.2초)을 사용합니다.
    }

    public override void Enter()
    {
        base.Enter();
        _stuckTimer = 0f;

        // EnvironmentScanner에서 기록해둔 '소리가 난 위치(LastSeenPosition)'를 가져옵니다.
        _investigateTarget = owner.scanner.LastSeenPosition;

        owner.navAgent.enabled = true;
        owner.navAgent.isStopped = false;

        // 순찰보다는 빠르고, 추격보다는 살짝 느린 '경계 걸음' 속도로 세팅합니다.
        owner.navAgent.speed = data.patrolSpeed * 1.5f;

        owner.navAgent.SetDestination(_investigateTarget);

        Debug.Log($"<color=yellow>[Investigate]</color> 의심스러운 지점({_investigateTarget})으로 조사를 시작합니다.");

        // [TODO] 경계하며 걷는 애니메이션 트리거가 있다면 여기서 호출
    }

    public override void Exit()
    {
        base.Exit();
        // 상태를 나갈 때 애니메이션 초기화 등
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 목적지 도착 여부 및 끼임(Stuck)을 확인합니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        if (owner.CurrentStateNet.Value != MonsterStateType.Investigate) return;

        // 1. 목적지(소리 난 곳)에 도착했는지 확인
        if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= owner.navAgent.stoppingDistance + 0.5f)
        {
            Debug.Log("<color=yellow>[Investigate]</color> 소리 진원지에 도착했습니다. 주변 수색을 시작합니다.");
            // 도착했는데 플레이어가 없다면 수색(Search) 상태로 전환하여 두리번거림
            owner.ChangeState(MonsterStateType.Search);
            return;
        }

        // 2. 지형에 끼었는지 확인 (2초 이상 막혀있으면 포기하고 수색 전환)
        if (owner.navAgent.velocity.sqrMagnitude < 0.1f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 2.0f)
            {
                owner.ChangeState(MonsterStateType.Search);
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

    /// <summary>
    /// 0.2초마다 실행: 조사하러 가는 도중에 플레이어를 눈으로 발견하는지 체크합니다.
    /// </summary>
    protected override void OnTick()
    {
        owner.scanner.Tick();

        // 1. 가는 도중에 플레이어가 시야에 들어왔다! -> 즉시 감지/추격 전환
        if (owner.scanner.CurrentTarget != null)
        {
            owner.ChangeState(MonsterStateType.Detect);
            return;
        }

        // 2. 닫힌 문이 앞을 가로막고 있다면 문을 엽니다.
        if (owner.CheckAndHandleDoor()) return;
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 미사용

    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    // 미사용
}