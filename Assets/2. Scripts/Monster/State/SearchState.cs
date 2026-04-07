using UnityEngine;

public class SearchState : MonsterBaseState
{
    private float searchTimer;
    private float searchDuration = 5f; // 5초간 수색

    public SearchState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        searchTimer = 0f;
        owner.navAgent.speed = data.patrolSpeed;
        // 마지막으로 플레이어를 본 위치로 이동
        owner.navAgent.SetDestination(owner.scanner.LastSeenPosition);
    }

    public override void Update()
    {
        // 수색 중 다시 발견하면 추격
        owner.scanner.Tick();
        if (owner.scanner.CurrentTarget != null)
        {
            owner.ChangeState(MonsterStateType.Chase);
            return;
        }

        searchTimer += Time.deltaTime;

        // 목적지 도착 후 두리번거리기 연출
        if (owner.navAgent.remainingDistance < 0.2f)
        {
            // 좌우로 회전하는 로직 등을 여기에 추가 (애니메이션 파라미터 활용 가능)
        }

        if (searchTimer >= searchDuration)
        {
            owner.ChangeState(MonsterStateType.Patrol);
        }
    }
}