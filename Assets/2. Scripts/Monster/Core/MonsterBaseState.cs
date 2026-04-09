using UnityEngine;

public abstract class MonsterBaseState : IState
{
    protected MonsterController owner;
    protected MonsterData data;

    private float tickTimer;
    // 상태마다 주기를 다르게 설정할 수 있도록 변수화
    protected float currentTickInterval;

    public MonsterBaseState(MonsterController owner)
    {
        this.owner = owner;
        this.data = owner.monsterData;
        this.currentTickInterval = data.aiTickInterval;
    }

    public virtual void Enter() 
    {
        tickTimer = 0f;
    }

    public virtual void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer >= currentTickInterval)
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