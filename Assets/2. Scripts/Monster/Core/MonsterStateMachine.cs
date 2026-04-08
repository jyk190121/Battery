using System.Collections.Generic;

public class MonsterStateMachine
{
    public IState CurrentState { get; private set; }    // 현재 실행 중인 상태

    // 상태 전환 메서드
    public void ChangeState(IState newState)
    {
        if (CurrentState == newState) return;
            
        CurrentState?.Exit();       // 기존 상태 종료 로직 실행
        CurrentState = newState;    // 상태 교체
        CurrentState.Enter();       // 새 상태 진입 로직 실행
    }

    public void Update() => CurrentState?.Update();
    public void FixedUpdate() => CurrentState?.FixedUpdate();
}