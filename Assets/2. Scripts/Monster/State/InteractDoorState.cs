using UnityEngine;

public class InteractDoorState : MonsterBaseState
{
    private float waitTimer;
    private float randomWaitDuration;

    public InteractDoorState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        owner.navAgent.isStopped = true; // 문 앞에서 정지
        waitTimer = 0f;

        // 2.0초 ~ 4.0초 사이의 랜덤한 대기 시간 설정
        randomWaitDuration = Random.Range(2.0f, 4.0f);

        Debug.Log($"[몬스터] 닫힌 문 앞 도착. {randomWaitDuration:F1}초간 문 열기 시도 (쾅쾅!)");
        // 여기서 문 쾅쾅 치는 애니메이션/사운드를 재생하면 좋음
    }

    public override void Update()
    {
        waitTimer += Time.deltaTime;

        // 랜덤 대기 시간이 끝나면 문 상태를 확인하고 행동 결정
        if (waitTimer >= randomWaitDuration)
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
}