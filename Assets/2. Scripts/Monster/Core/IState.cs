public interface IState
{
    void Enter();              // 상태 진입 시 1회 실행
    void Update();             // 매 프레임 실행 (Logic)
    void FixedUpdate();        // 물리 및 AI 판단 (Server Only 추천)
    void Exit();               // 상태 종료 시 1회 실행
}