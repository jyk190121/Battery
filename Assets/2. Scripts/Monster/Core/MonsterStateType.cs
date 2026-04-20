public enum MonsterStateType
{
    Idle,          // 정지
    Patrol,        // 순찰
    Detect,        // 감지
    Chase,         // 추격
    Attack,        // 공격
    Search,        // 찾기
    Stunned,       // 스턴
    InteractDoor,  // 문 열기
    Dead,          // 사망
    Investigate,    // 조사 (사운드)

    // 올무벼룩 전용 상태
    CeilingWait,   // 천장에 붙어 대기하는 상태
    Attached,      // 플레이어 머리에 붙어 공격하는 상태
    Flee           // 공격받아 떨어졌을 때 도망가는 상태
}