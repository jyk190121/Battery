using UnityEngine;

public class DeadState : MonsterBaseState
{
    public DeadState(MonsterController owner) : base(owner) { }
    public override void Enter()
    {
        owner.navAgent.isStopped = true;
        owner.animHandler.SetVisualSpeed(0, 0, 0, MonsterStateType.Dead);
        Debug.Log("몬스터 사망.");
        // 여기서 콜라이더를 끄거나 시체 래그돌을 처리
    }
}