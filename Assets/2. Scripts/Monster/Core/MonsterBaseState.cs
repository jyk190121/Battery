using UnityEngine;

public abstract class MonsterBaseState : IState
{
    protected MonsterController owner;
    protected MonsterData data;

    private float tickTimer;
    // 상태마다 주기를 다르게 설정할 수 있도록 변수화
    protected float currentTickInterval;    // AI 사고 주기 (최적화)

    public MonsterBaseState(MonsterController owner)
    {
        this.owner = owner;
        this.data = owner.monsterData;
        this.currentTickInterval = data.aiTickInterval;
    }

    public virtual void Enter() 
    {
        // 여러 마리의 몬스터가 동일한 프레임에 동시에 연산하는 현상(CPU 스파이크)을 방지
        tickTimer = Random.Range(0f, currentTickInterval);
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