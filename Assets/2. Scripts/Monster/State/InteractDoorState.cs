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

        
        // CanOpenWithoutKey를 활용한 조건 분기
        if (owner.TargetDoor.CanOpenWithoutKey)
        {
            // 1. 잠기지 않은 일반 문일 경우
            owner.TargetDoor.TryOpen("");
            owner.animHandler.SetSpeed(1f);
            Debug.Log("문을 열었습니다. 추격 재개");
            owner.ChangeState(MonsterStateType.Chase);
        }
        else
        {
            // 2. 열쇠가 필요한 잠긴 문일 경우
            Debug.Log("문이 잠겨있습니다. 추격을 포기하고 수색합니다.");
            owner.ChangeState(MonsterStateType.Search);
        }

        // 타겟 문 데이터 초기화
        owner.TargetDoor = null;
    }

    public override void Exit() { }
}