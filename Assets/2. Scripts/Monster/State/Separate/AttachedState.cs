using UnityEngine;
using Unity.Netcode;

public class AttachedState : MonsterBaseState
{
    private PlayerController snaredPlayer;
    private float damageTimer;

    public AttachedState(MonsterController owner) : base(owner)
    {
        this.currentTickInterval = 0.1f;
    }

    public override void Enter()
    {
        base.Enter();
        damageTimer = 0f;
        snaredPlayer = null;

        Transform target = owner.scanner.CurrentTarget;
        if (target != null)
        {
            snaredPlayer = target.GetComponentInParent<PlayerController>();
        }

        // 1. 타겟이 유효한지 1차 검사
        if (snaredPlayer != null && !snaredPlayer.IsDead)
        {
            Debug.Log($"<color=red>[Snare Flea]</color> {snaredPlayer.name}의 머리에 안착했습니다!");

            // 2. [네트워크 종속] 몬스터를 플레이어의 자식(Child)으로 설정 (worldPositionStays = false)
            owner.NetworkObject.TrySetParent(snaredPlayer.NetworkObject, false);

            // 3. 플레이어 머리 위치(대략 높이 1.6m)로 로컬 위치 고정
            owner.transform.localPosition = new Vector3(0, 1.6f, 0);
            owner.transform.localRotation = Quaternion.identity;

            // 4. RpcTarget.Single()을 사용하여 타겟 클라이언트에게만 시야 차단 지시
            owner.TriggerSnareBlindRpc(true, owner.RpcTarget.Single(snaredPlayer.OwnerClientId, RpcTargetUse.Temp));
        }
        else
        {
            // 타겟이 이미 도망갔거나 죽었다면 즉시 순찰로 복귀
            owner.ChangeState(MonsterStateType.Patrol);
        }
    }

    public override void Update()
    {
        base.Update();

        // 1. 사망/로그아웃 체크
        if (snaredPlayer == null || snaredPlayer.IsDead)
        {
            Debug.Log("<color=yellow>[Snare Flea]</color> 숙주가 사망했습니다. 바닥으로 떨어집니다.");

            // 화면 복구 및 부모 해제 로직은 Exit()에 일괄 작성되어 있으므로 상태 변경만 호출합니다.
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 2. 지속 데미지 
        damageTimer += Time.deltaTime;

        if (damageTimer >= data.snareTickRate)
        {
            damageTimer = 0f;

            // 데미지 처리를 서버로 요청 (MonsterData의 snareTickDamage 사용)
            snaredPlayer.TakeDamageServerRpc(data.snareTickDamage);

            Debug.Log($"<color=red>[Snare Flea]</color> 목 조르기! (데미지: {data.snareTickDamage})");
        }
    }

    public override void Exit()
    {
        base.Exit();

        if (snaredPlayer != null)
        {
            // 1. 관전 화면이 먹통이 되지 않도록 확실하게 시야 차단 해제(false)
            owner.TriggerSnareBlindRpc(false, owner.RpcTarget.Single(snaredPlayer.OwnerClientId, RpcTargetUse.Temp));
        }

        // 2. [네트워크 종속 해제] 플레이어 머리에서 떨어짐
        if (owner.NetworkObject.TryRemoveParent())
        {
            // 떨어질 때 바닥에 똑바로 안착할 수 있도록 회전값과 Y축 높이를 리셋합니다.
            owner.transform.rotation = Quaternion.Euler(0, owner.transform.eulerAngles.y, 0);
        }

        snaredPlayer = null;
        Debug.Log("<color=yellow>[Snare Flea]</color> 플레이어에게서 분리되었습니다.");
    }
}