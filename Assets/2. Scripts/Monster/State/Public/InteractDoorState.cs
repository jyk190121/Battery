using UnityEngine;

public class InteractDoorState : MonsterBaseState
{
    private float waitTimer;
    private float currentDuration;

    public InteractDoorState(MonsterController owner) : base(owner) 
    {
        this.currentTickInterval = data.fastTickInterval;
    }

    public override void Enter()
    {
        base.Enter();
        owner.navAgent.isStopped = true; // 문 앞에서 정지
        waitTimer = 0f;

        currentDuration = Random.Range(0.8f, 2.0f);
    }

    protected override void OnTick()
    {
        // 문이 이미 열렸으면 즉시 추격으로 전환
        if (owner.TargetDoor != null && owner.TargetDoor.isOpen)
        {
            owner.ChangeState(MonsterStateType.Chase);
        }
    }

    public override void Update()
    {
        base.Update();
        waitTimer += Time.deltaTime;

        // 랜덤 대기 시간이 끝나면 문 상태를 확인하고 행동 결정
        if (waitTimer >= currentDuration)
        {
            ProcessDoorInteraction();
        }
    }

    private void ProcessDoorInteraction()
    {
        if (owner.TargetDoor == null)
        {
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 문을 열 수 있는 상황일 때
        if (owner.TargetDoor.CanOpenWithoutKey)
        {
            owner.TargetDoor.TryOpen("");

            // 타겟 유무와 이전 상태에 따른 지능적 상태 전환

            // 1. 현재 시야에 타겟이 확실히 있는 경우 -> 추격 재개
            if (owner.scanner.CurrentTarget != null)
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 타겟이 보이므로 추격을 재개합니다.");
                owner.ChangeState(MonsterStateType.Chase);
            }
            // 2. 타겟은 없지만, 이전에 수색 중이었다면 -> 수색 계속 (마지막 위치로 이동)
            else if (owner.PreviousState == MonsterStateType.Search)
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 수색 중이었으므로 수색을 계속합니다.");
                owner.ChangeState(MonsterStateType.Search);
            }
            // 3. 그 외 (순찰 중이었거나 타겟을 완전히 잃은 경우) -> 정찰 복귀
            else
            {
                Debug.Log("<color=cyan>[Door]</color> 문을 열었습니다. 특별한 타겟이 없으므로 정찰로 복귀합니다.");
                owner.ChangeState(MonsterStateType.Patrol);
            }
        }
        else
        {
            // 열쇠가 필요한 잠긴 문일 경우 -> 추격을 포기하고 주변 수색
            Debug.Log("<color=orange>[Door]</color> 문이 잠겨있습니다. 추격을 포기하고 주변을 수색합니다.");
            owner.ChangeState(MonsterStateType.Search);
        }

        // 타겟 문 데이터 초기화
        owner.TargetDoor = null;
    }

    public override void Exit() { }
}