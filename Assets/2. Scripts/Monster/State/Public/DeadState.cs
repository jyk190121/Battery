using UnityEngine;
using UnityEngine.AI;

public class DeadState : MonsterBaseState
{
    public DeadState(MonsterController owner) : base(owner)
    {
        // 시체는 주변을 스캔할 필요가 없으므로 틱 연산 주기를 무한에 가깝게 설정해 버립니다.
        this.currentTickInterval = 999f;
    }

    public override void Enter()
    {
        base.Enter();

        Debug.Log($"<color=gray>[DeadState]</color> {owner.gameObject.name} 사망 처리 시작");

        // 1. 이동(네비게이션) 완전 정지 및 비활성화
        if (owner.navAgent != null && owner.navAgent.enabled)
        {
            owner.navAgent.isStopped = true;
            owner.navAgent.enabled = false;
        }

        // 2. 주변 감지(Scanner) 비활성화
        if (owner.scanner != null)
        {
            owner.scanner.enabled = false;
        }

        // 3. 콜라이더(물리 충돌) 해제 
        Collider[] colliders = owner.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // 4. 애니메이션 실행 (서버/클라이언트 모두 시각적 처리가 필요함)
        if (owner.animHandler != null)
        {
            owner.animHandler.TriggerDeath(); 
        }
        else
        {
            if (owner.IsServer)
            {
                owner.transform.rotation *= Quaternion.Euler(0, 0, 90f);
            }
        }

        // 5. 서버 한정: 몬스터 매니저에서 완전히 등록 해제
        if (owner.IsServer && EnemyManager.Instance != null)
        {
            EnemyManager.Instance.UnregisterEnemy(owner.monsterData);
        }
    }

    public override void Update()
    {
    }

    protected override void OnTick()
    {
    }

    public override void Exit()
    {
        base.Exit();
    }
}