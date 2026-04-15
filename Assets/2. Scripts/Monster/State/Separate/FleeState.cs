using UnityEngine;

public class FleeState : MonsterBaseState
{
    public FleeState(MonsterController owner) : base(owner) { }
    public override void Enter()
    {
        owner.navAgent.enabled = true;
        owner.navAgent.speed = data.chaseSpeed * 1.5f; // 겁나 빨리 도망
        Debug.Log("도망가는 중!");
    }
}