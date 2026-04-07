public abstract class MonsterBaseState : IState
{
    protected MonsterController owner;
    protected MonsterData data;

    public MonsterBaseState(MonsterController owner)
    {
        this.owner = owner;
        this.data = owner.monsterData;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}