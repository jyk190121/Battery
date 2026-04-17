using UnityEngine;

public class StunnedState : MonsterBaseState
{
    private float stunTimer;

    public StunnedState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();
        stunTimer = 0f;

        Debug.Log($"<color=cyan>[StunnedState]</color> {owner.gameObject.name} 스턴 시작! (목표 시간: {owner.CurrentStunDuration}초)");

        // 1. 발 묶기 (완전 정지 및 미끄러짐 방지)
        if (owner.navAgent != null && owner.navAgent.isActiveAndEnabled && owner.navAgent.isOnNavMesh)
        {
            owner.navAgent.isStopped = true;
            owner.navAgent.ResetPath();
            owner.navAgent.velocity = Vector3.zero;
        }

        // 2. 어그로/타겟 초기화 (눈뽕을 맞았으니 쫓던 대상을 잃어버림)
        if (owner.scanner != null)
        {
            owner.scanner.SetForceTarget(null);
        }

        // 3. 스턴 애니메이션 재생 (추후 애니메이터 세팅이 완료되면 주석 해제하세요!)
        // if (owner.animHandler != null)
        // {
        //     owner.animHandler.PlayStun();
        // }
    }

    public override void Update()
    {
        base.Update();

        stunTimer += Time.deltaTime;

        // 지정된 스턴 시간이 다 끝나면?
        if (stunTimer >= owner.CurrentStunDuration)
        {
            // 두리번거리며 타겟을 다시 찾는 상태(Search)로 복귀
            owner.ChangeState(MonsterStateType.Search);
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 스턴이 끝날 때 애니메이션 락을 풀어주거나 하는 추가 작업이 필요하면 여기에 작성합니다.

        Debug.Log($"<color=cyan>[StunnedState]</color> {owner.gameObject.name} 스턴 종료! 다음 상태로 넘어갑니다.");
    }
}