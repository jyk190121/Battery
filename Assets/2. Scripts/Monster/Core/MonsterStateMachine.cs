using System.Collections.Generic;

public class MonsterStateMachine
{
    public IState CurrentState { get; private set; }

    // 상태 전환 메서드
    public void ChangeState(IState newState)
    {
        if (CurrentState == newState) return;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update() => CurrentState?.Update();
    public void FixedUpdate() => CurrentState?.FixedUpdate();
}