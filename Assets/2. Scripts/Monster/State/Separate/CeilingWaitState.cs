using UnityEngine;
using Unity.Netcode;

public class CeilingWaitState : MonsterBaseState
{
    private bool isAttachedToCeiling = false;

    private RaycastHit[] hits = new RaycastHit[10];

    public CeilingWaitState(MonsterController owner) : base(owner)
    {
        this.currentTickInterval = 0.05f;
    }

    public override void Enter()
    {
        base.Enter();
        isAttachedToCeiling = false;

        owner.navAgent.enabled = false;

        if (Physics.Raycast(owner.transform.position, Vector3.up, out RaycastHit hit, data.ceilingCheckDistance, data.ceilingLayerMask))
        {
            // 천장 바로 아래에 살짝 띄워서 위치를 고정 (모델링에 따라 offset 조절 필요)
            Vector3 ceilingPos = hit.point - new Vector3(0, 0.5f, 0);
            owner.transform.position = ceilingPos;

            owner.transform.rotation = Quaternion.Euler(180f, owner.transform.eulerAngles.y, 0f);

            isAttachedToCeiling = true;
            Debug.Log("<color=yellow>[Snare Flea]</color> 천장에 성공적으로 안착했습니다. 사냥감을 기다립니다.");
        }
        else
        {

            Debug.Log("<color=yellow>[Snare Flea]</color> 천장이 없어서 다시 바닥으로 내려옵니다.");
            owner.ChangeState(MonsterStateType.Patrol);
        }
    }

    protected override void OnTick()
    {
        if (!owner.IsServer || !isAttachedToCeiling) return;

        int hitCount = Physics.SphereCastNonAlloc(
            owner.transform.position,
            data.dropTriggerRadius,
            Vector3.down,
            hits, 
            data.ceilingCheckDistance
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hits[i].collider;
            PlayerController player = col.GetComponentInParent<PlayerController>();

            if (player != null && !player.IsDead)
            {
                // 장애물 검사 (천장과 플레이어 사이에 막힌 구조물 확인)
                Vector3 playerChestPos = player.transform.position + Vector3.up * 1.0f;
                Vector3 dropDir = (playerChestPos - owner.transform.position);
                float dropDist = dropDir.magnitude;

                if (!Physics.Raycast(owner.transform.position, dropDir.normalized, dropDist, data.ceilingLayerMask))
                {
                    Debug.Log($"<color=red>[Snare Flea]</color> {player.name} 포착. 경로에 장애물 없음. 낙하 시작");

                    owner.scanner.SetForceTarget(player.transform);
                    owner.ChangeState(MonsterStateType.Attached);
                    return;
                }
            }
        }
    }

    public override void Exit()
    {
        base.Exit();
        owner.transform.rotation = Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f);
    }
}