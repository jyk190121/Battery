using UnityEngine;

/// <summary>
/// 몬스터가 플레이어를 사거리 내에 포착했을 때 실행되는 공격 상태입니다.
/// </summary>
public class AttackState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private float _attackTimer;
    private bool _isAnimationPlaying;
    private bool _isExiting;

    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public AttackState(MonsterController owner) : base(owner)
    {
        // 공격은 찰나의 순간이 중요하므로 0.05초 주기로 빠르게 판단하게 합니다.
        this.currentTickInterval = data.fastTickInterval;
    }

    public override void Enter()
    {
        base.Enter();

        // 1. 공격 시 미끄러지지 않도록 제자리에 멈춤
        owner.navAgent.isStopped = true;
        owner.navAgent.ResetPath();
        owner.navAgent.velocity = Vector3.zero;

        // 2. 변수 초기화 (즉시 공격 가능하도록 쿨타임부터 시작)
        _attackTimer = data.attackCooldown;
        _isAnimationPlaying = false;
        _isExiting = false;
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 시각적으로 부드러워야 하는 회전 및 애니메이션 트리거 담당
    /// </summary>
    public override void Update()
    {
        base.Update();
        _attackTimer += Time.deltaTime;

        Transform target = owner.scanner.CurrentTarget;

        // 1. 유도 회전: 공격 애니메이션 재생 시간 동안은 타겟을 계속 쳐다봄
        if (_attackTimer < data.attackAnimDuration)
        {
            if (target != null)
            {
                LookAtTarget(target);
            }
        }

        // 2. 쿨타임이 찼고, 아직 애니메이션 재생 전이라면 공격 실행
        if (!_isAnimationPlaying && _attackTimer >= data.attackCooldown)
        {
            ExecuteAttack();
        }
    }

    /// <summary>
    /// 0.05초마다 실행: 타겟이 사거리를 벗어났는지 확인하는 AI 두뇌 역할
    /// </summary>
    protected override void OnTick()
    {
        if (_isExiting || owner.CurrentStateNet.Value != MonsterStateType.Attack) return;

        Transform target = owner.scanner.CurrentTarget;

        // 타겟이 죽거나 사라졌다면 즉시 추격(또는 수색)으로 전환
        if (target == null)
        {
            ExitToChase();
            return;
        }

        // 한 번의 공격 사이클(애니메이션)이 끝난 직후 사거리 재평가
        if (_attackTimer >= data.attackAnimDuration)
        {
            float sqrDist = (target.position - owner.transform.position).sqrMagnitude;

            // 여유 거리 0.5f를 두어, 너무 칼같이 취소되는 것을 방지
            float threshold = data.attackRange + 0.5f;

            // 플레이어가 도망가서 사거리 밖이라면 추격 전환
            if (sqrDist > threshold * threshold)
            {
                ExitToChase();
                return;
            }
            else
            {
                // 여전히 사거리 안에 있다면 다음 공격을 위해 락(Lock) 해제
                _isAnimationPlaying = false;
            }
        }
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    /// <summary>
    /// MonsterController에서 애니메이션 이벤트 발생 시 호출하는 데미지 전달 함수입니다.
    /// </summary>
    public void ApplyDamageToTarget()
    {
        Transform target = owner.scanner.CurrentTarget;
        if (target == null) return;

        // 실제 데미지가 들어가는 순간의 판정 범위 (기본 사거리보다 약간 넉넉하게)
        float hitThreshold = data.attackRange + 1.0f;

        if ((target.position - owner.transform.position).sqrMagnitude <= hitThreshold * hitThreshold)
        {
            if (target.TryGetComponent<PlayerController>(out var player))
            {
                player.TakeDamageServerRpc(data.attackDamage);
                Debug.Log($"<color=red>[데미지 발생]</color> {player.name}에게 {data.attackDamage} 피해 전달됨");
            }
        }
    }


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 타겟 방향으로 부드럽게 몸을 회전시킵니다.
    /// </summary>
    private void LookAtTarget(Transform target)
    {
        Vector3 dir = (target.position - owner.transform.position).normalized;

        // Y축 회전만 허용하여 몬스터가 위아래로 기울어지는 것 방지
        Quaternion targetRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
        owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, targetRot, Time.deltaTime * 8f);
    }

    /// <summary>
    /// 실제 공격 애니메이션을 트리거하고 타이머를 초기화합니다.
    /// </summary>
    private void ExecuteAttack()
    {
        _isAnimationPlaying = true;
        _attackTimer = 0f; // 리셋 후 OnTick 및 Update 연산 재개

        owner.animHandler.PlayAttack();
    }

    /// <summary>
    /// 안전하게 추격 상태로 넘어가는 헬퍼 함수입니다.
    /// </summary>
    private void ExitToChase()
    {
        _isExiting = true;
        owner.ChangeState(MonsterStateType.Chase);
    }
}