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

        // [확인 1] 상태 진입 확인용 디버그! (이게 안 뜨면 아예 안 들어온 겁니다)
        Debug.Log("<color=cyan>[CeilingWait]</color> 상태 진입 성공! 천장 검사를 시작합니다.");

        isAttachedToCeiling = false;
        owner.navAgent.enabled = false;

        // 발사 지점을 가슴 높이로 올림
        Vector3 rayStartPos = owner.transform.position + Vector3.up * 1.5f;

        // [핵심 수정] 여기에 owner.transform.position 대신 rayStartPos를 쏙 넣어야 합니다!
        if (Physics.Raycast(rayStartPos, Vector3.up, out RaycastHit hit, data.ceilingCheckDistance, data.ceilingLayerMask))
        {
            float ceilingHeight = hit.point.y - owner.transform.position.y;

            if (ceilingHeight >= 2.5f)
            {
                Vector3 ceilingPos = hit.point - new Vector3(0, 0.5f, 0);
                owner.transform.position = ceilingPos;
                owner.transform.rotation = Quaternion.Euler(180f, owner.transform.eulerAngles.y, 0f);

                isAttachedToCeiling = true;
                Debug.Log($"<color=yellow>[Snare Flea]</color> 진짜 천장({ceilingHeight:F1}m)에 안착했습니다.");
            }
            else
            {
                Debug.Log($"<color=yellow>[Snare Flea]</color> 장애물({ceilingHeight:F1}m)이 너무 낮아 천장이 아닙니다.");
            }
        }
        else
        {
            Debug.Log("<color=yellow>[Snare Flea]</color> 머리 위에 천장이 없거나 너무 높습니다.");
        }
    }

    public override void Update()
    {
        base.Update();

        if (!owner.IsServer) return;

        // 천장에 붙지 못했다면 즉시 순찰로 복귀
        if (!isAttachedToCeiling)
        {
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }
    }

    protected override void OnTick()
    {
        if (!owner.IsServer || !isAttachedToCeiling) return;

        foreach (var player in PlayerController.AllPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.isDead.Value) continue;

            // 1. 수평 거리와 수직 거리 체크 (벼룩 아래에 있는지 확인)
            Vector3 fleaPos = owner.transform.position;
            Vector3 playerPos = player.transform.position;

            // 벼룩과 플레이어의 수평 거리 (XZ 평면 거리)
            float horizontalDist = Vector2.Distance(new Vector2(fleaPos.x, fleaPos.z), new Vector2(playerPos.x, playerPos.z));
            // 벼룩과 플레이어의 수직 높이 차이
            float heightDiff = fleaPos.y - playerPos.y;

            // 설정한 반경(DropTriggerRadius) 안에 있고, 너무 멀리 떨어져 있지 않은지 확인
            if (horizontalDist <= data.dropTriggerRadius && heightDiff > 0 && heightDiff <= data.ceilingCheckDistance)
            {
                // 2. 장애물 검사 (천장과 플레이어 사이가 막혔는지 확인)
                // 시작점을 벼룩 위치보다 약간 아래(Vector3.down * 0.5f)에서 쏴서 천장 충돌을 피합니다.
                Vector3 rayStart = fleaPos + Vector3.down * 0.8f;
                Vector3 playerChestPos = player.transform.position + Vector3.up * 1.0f;
                Vector3 dropDir = (playerChestPos - rayStart);
                float dropDist = dropDir.magnitude;

                // 디버그 레이저 (Scene 뷰에서 확인용)
                Debug.DrawRay(rayStart, dropDir.normalized * dropDist, Color.red, 1.0f);

                if (!Physics.Raycast(rayStart, dropDir.normalized, dropDist, data.ceilingLayerMask))
                {
                    Debug.Log($"<color=red>[Snare Flea]</color> {player.name} 포착! 수직 낙하합니다.");

                    owner.scanner.SetForceTarget(player.transform);
                    owner.ChangeState(MonsterStateType.Attached);
                    return;
                }
                else
                {
                    Debug.Log("<color=gray>[Snare Flea]</color> 플레이어를 감지했으나 장애물에 가려져 있습니다.");
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