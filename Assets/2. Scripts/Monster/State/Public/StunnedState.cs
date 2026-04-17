using UnityEngine;

public class StunnedState : MonsterBaseState
{
    private float stunTimer;

    public StunnedState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();
        stunTimer = 0f;

        // 1. 발 묶기 (완전 정지)
        if (owner.navAgent != null && owner.navAgent.isActiveAndEnabled && owner.navAgent.isOnNavMesh)
        {
            owner.navAgent.isStopped = true;
            owner.navAgent.ResetPath();
            owner.navAgent.velocity = Vector3.zero;
        }

        // 2. 어그로/타겟 초기화 (선택 사항: 섬광탄에 맞으면 쫓던 타겟을 잃어버리게 함)
        owner.scanner.SetForceTarget(null);

        // 3. 스턴 애니메이션 재생 (MonsterAnimation 스크립트에 PlayStun() 함수를 만들어두세요!)
        if (owner.animHandler != null)
        {
            // owner.animHandler.PlayStun();
        }
    }

    public override void Update()
    {
        base.Update();

        stunTimer += Time.deltaTime;

        // 지정된 스턴 시간이 다 끝나면?
        if (stunTimer >= owner.currentStunDuration)
        {
            // 주변을 두리번거리는 상태(Search)나 다시 순찰(Patrol) 상태로 복귀
            owner.ChangeState(MonsterStateType.Search);
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 스턴이 끝날 때 애니메이션 락을 풀어주거나 하는 추가 작업이 필요하면 여기에 작성
    }
}