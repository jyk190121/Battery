using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 체력이 0이 되었을 때 진입하는 사망 상태입니다.
/// 모든 AI 연산과 물리 충돌을 정지시키고 시체 처리 연출을 담당합니다.
/// </summary>
public class DeadState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    // 본 상태에서는 추가적인 상태 변수를 사용하지 않습니다.


    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public DeadState(MonsterController owner) : base(owner)
    {
        // 시체는 주변을 스캔하거나 AI 판단을 할 필요가 없으므로, 
        // 틱 연산 주기를 무한(999초)에 가깝게 늘려 CPU 점유율을 사실상 0으로 만듭니다.
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

        // 3. 콜라이더(물리 충돌) 해제 (시체에 플레이어가 걸려 넘어지지 않도록 함)
        DisableAllColliders();

        // 4. 애니메이션 실행 (서버/클라이언트 모두 시각적 처리가 필요함)
        PlayDeathAnimation();

        // 5. 서버 한정: 몬스터 매니저에서 완전히 등록 해제하여 스폰 예산(Budget) 반환
        if (owner.IsServer && EnemyManager.Instance != null)
        {
            EnemyManager.Instance.UnregisterEnemy(owner.monsterData, owner.NetworkObject);
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 시체는 보통 파괴되거나 풀(Pool)로 돌아가므로 특별한 복구 로직이 필요하지 않습니다.
        // 상태 초기화는 MonsterController의 ResetMonsterState()에서 일괄 처리합니다.
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 시체는 움직이거나 상호작용하지 않으므로 Update 연산을 완전히 생략합니다. 
    /// </summary>
    public override void Update()
    {
        // 의도적으로 비워둠
    }

    /// <summary>
    /// 시체는 생각을 하지 않으므로 AI 틱 연산을 완전히 생략합니다. 
    protected override void OnTick()
    {
        // 의도적으로 비워둠
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 몬스터 본체 및 자식 객체에 붙어있는 모든 콜라이더를 끕니다.
    /// </summary>
    private void DisableAllColliders()
    {
        Collider[] colliders = owner.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    /// <summary>
    /// 사망 애니메이션을 재생하거나, 애니메이터가 없을 경우 90도 눕혀버립니다.
    /// </summary>
    private void PlayDeathAnimation()
    {
        if (owner.animHandler != null)
        {
            owner.animHandler.TriggerDeath();
        }
        else
        {
            // 애니메이터가 없는 임시 모델일 경우 물리적으로 눕힘
            // (NetworkTransform이 붙어있다고 가정하여 서버에서만 회전시킵니다)
            if (owner.IsServer)
            {
                owner.transform.rotation *= Quaternion.Euler(0, 0, 90f);
            }
        }
    }
}