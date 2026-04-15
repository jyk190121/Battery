using UnityEngine;

public class AttachedState : MonsterBaseState
{
    public AttachedState(MonsterController owner) : base(owner) { }
    public override void Enter() { Debug.Log("플레이어 머리에 부착됨!"); }
}
