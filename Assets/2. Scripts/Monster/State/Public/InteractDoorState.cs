using UnityEngine;

/// <summary>
/// 몬스터가 닫힌 문 앞을 가로막혔을 때 진입하는 상호작용(문 열기) 상태입니다.
/// 잠시 대기한 후 문을 열고, 이전 상태(추격/수색/순찰)에 맞춰 다음 행동을 지능적으로 결정합니다.
/// </summary>
public class InteractDoorState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private float _waitTimer;
    private float _currentDuration;


    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public InteractDoorState(MonsterController owner) : base(owner)
    {
        // 문 앞에서 머뭇거리는(상호작용) 순간은 빠른 판단이 필요하므로 짧은 주기로 설정
        this.currentTickInterval = data.fastTickInterval;
    }

    public override void Enter()
    {
        base.Enter();

        // 1. 문을 열기 위해 제자리에 멈춰 섭니다.
        owner.navAgent.isStopped = true;
        owner.navAgent.velocity = Vector3.zero;

        // 2. 타이머 초기화 (0.8초 ~ 2초 사이의 랜덤한 시간 동안 문을 쾅쾅 두드리거나 대기)
        _waitTimer = 0f;
        _currentDuration = Random.Range(0.8f, 2.0f);

        // [TODO] 문을 두드리는 애니메이션 재생
    }

    public override void Exit()
    {
        base.Exit();
        // 상태를 벗어날 때 필요한 초기화가 있다면 여기에 작성 (현재는 비워둠)
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 대기 타이머를 계산하고 시간이 다 되면 문과 상호작용합니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        _waitTimer += Time.deltaTime;

        // 랜덤 대기 시간이 끝나면 문 상태를 확인하고 행동 결정
        if (_waitTimer >= _currentDuration)
        {
            ProcessDoorInteraction();
        }
    }

    /// <summary>
    /// 빠른 주기(0.05초)로 실행: 다른 플레이어나 몬스터가 문을 먼저 열어주었는지 감시합니다.
    /// </summary>
    protected override void OnTick()
    {
        // 내가 문을 열기 전에 이미 누군가(또는 스스로 열리는 문) 문을 열었다면 대기 취소 후 즉시 추격 전환
        if (owner.TargetDoor != null && owner.TargetDoor.isOpen)
        {
            owner.ChangeState(MonsterStateType.Chase);
        }
    }


    // =========================================================
    // 4. 퍼블릭 함수
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 문을 열어젖히고 다음 행동(상태)을 지능적으로 결정합니다.
    /// </summary>
    private void ProcessDoorInteraction()
    {
        // 예외 처리: 타겟 문 데이터가 유실된 경우 안전하게 순찰로 복귀
        if (owner.TargetDoor == null)
        {
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 1. 문을 열 수 있는 상황일 때 (열쇠가 필요 없는 문)
        if (owner.TargetDoor.CanOpenWithoutKey)
        {
            // 문 개방 시도
            owner.TargetDoor.TryOpen("");

            // 타겟 유무와 이전 상태(PreviousState)에 따른 지능적 상태 전환 로직

            // A. 현재 시야에 타겟이 확실히 있는 경우 -> 즉시 추격 재개
            if (owner.scanner.CurrentTarget != null)
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 타겟이 보이므로 추격을 재개합니다.");
                owner.ChangeState(MonsterStateType.Chase);
            }
            // B. 타겟은 안 보이지만, 방금 전까지 추격(Chase) 중이었다면
            else if (owner.PreviousState == MonsterStateType.Chase)
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 방금 전까지 쫓던 타겟을 향해 추격을 강행합니다.");
                owner.ChangeState(MonsterStateType.Chase);
            }
            // C. 방금 전까지 수색(Search) 중이었다면 -> 수색 계속 진행
            else if (owner.PreviousState == MonsterStateType.Search)
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 수색 중이었으므로 수색을 계속합니다.");
                owner.ChangeState(MonsterStateType.Search);
            }
            // D. 소리를 듣고 조사(Investigate) 중이었다면 -> 조사 계속 진행
            else if (owner.PreviousState == MonsterStateType.Investigate)
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 의심스러운 소리가 난 곳으로 조사를 계속합니다.");
                owner.ChangeState(MonsterStateType.Investigate);
            }
            // E. 그 외 (단순 순찰 중이었거나 어그로가 완전히 빠진 경우) -> 정찰 복귀
            else
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 특별한 타겟이 없으므로 정찰로 복귀합니다.");
                owner.ChangeState(MonsterStateType.Patrol);
            }
        }
        else
        {
            // 2. 열쇠가 필요한 굳게 잠긴 문일 경우 -> 뚫지 못하므로 추격을 포기하고 주변을 두리번거림
            Debug.Log("<color=orange>[Door]</color> 문이 잠겨있습니다. 진입을 포기하고 주변을 수색합니다.");
            owner.ChangeState(MonsterStateType.Search);
        }

        // 상호작용이 끝났으므로 타겟 문 데이터 초기화 (메모리 해제)
        owner.TargetDoor = null;
    }
}