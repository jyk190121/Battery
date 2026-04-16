using UnityEngine;
using Unity.Netcode;
using System.Collections;

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
        if (snaredPlayer != null && !snaredPlayer.isDead.Value)
        {
            // 1. 에이전트와 물리 충돌을 꺼서 윗층 점프 방지
            owner.navAgent.enabled = false;
            //if (owner.TryGetComponent<Collider>(out var col)) col.enabled = false;

            // 2. 부모 설정
            owner.NetworkObject.TrySetParent(snaredPlayer.NetworkObject, false);

            // 3. 한 프레임 뒤에 위치를 다시 고정 (부모 설정 싱크 때문)
            owner.StartCoroutine(FixPositionNextFrame());

            owner.TriggerSnareBlindRpc(true, owner.RpcTarget.Single(snaredPlayer.OwnerClientId, RpcTargetUse.Temp));
        }
        else
        {
            owner.ChangeState(MonsterStateType.Patrol);
        }
    }

    private IEnumerator FixPositionNextFrame()
    {
        yield return null;
        owner.transform.localPosition = new Vector3(0, 1.2f, 0); // 머리 위 높이
        owner.transform.localRotation = Quaternion.identity;
    }

    public override void Update()
    {
        base.Update();

        // 1. 사망/로그아웃 체크
        if (snaredPlayer == null || snaredPlayer.isDead.Value)
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

        if (snaredPlayer != null && snaredPlayer.IsSpawned)
        {
            owner.TriggerSnareBlindRpc(false, owner.RpcTarget.Single(snaredPlayer.OwnerClientId, RpcTargetUse.Temp));
        }

        // 2. [네트워크 종속 해제] 플레이어 머리에서 떨어짐
        if (owner.NetworkObject.TryRemoveParent())
        {
            // 떨어질 때 바닥에 똑바로 안착할 수 있도록 회전값과 Y축 높이를 리셋합니다.
            owner.transform.rotation = Quaternion.Euler(0, owner.transform.eulerAngles.y, 0);
        }

        owner.navAgent.enabled = owner.IsServer;
        //if (owner.TryGetComponent<Collider>(out var col)) col.enabled = true;

        snaredPlayer = null;
        Debug.Log("<color=yellow>[Snare Flea]</color> 플레이어에게서 분리되었습니다.");
    }
}