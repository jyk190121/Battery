using UnityEngine;

public abstract class MonsterBaseState : IState
{
    protected MonsterController owner;
    protected MonsterData data;

    private float tickTimer;

    public MonsterBaseState(MonsterController owner)
    {
        this.owner = owner;
        this.data = owner.monsterData;
    }

    public virtual void Enter() { }
    public virtual void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer >= data.aiTickInterval)
        {
            tickTimer = 0f;
            OnTick(); // 0.2초마다 실행될 함수
        }

    }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    // 자식 스크립트들이 "무거운 연산"을 작성할 곳
    protected virtual void OnTick() { }
}