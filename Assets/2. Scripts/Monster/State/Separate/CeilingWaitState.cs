using UnityEngine;

/// <summary>
/// 올무벼룩(Snare Flea)이 천장에 달라붙어 아래를 지나가는 플레이어를 암살하기 위해 대기하는 상태입니다.
/// </summary>
public class CeilingWaitState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private bool _isAttachedToCeiling = false;

    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public CeilingWaitState(MonsterController owner) : base(owner)
    {
        this.currentTickInterval = 0.05f;
    }

    public override void Enter()
    {
        base.Enter();

        Debug.Log("<color=cyan>[CeilingWait]</color> 상태 진입 성공! 천장 검사를 시작합니다.");

        _isAttachedToCeiling = false;
        owner.navAgent.enabled = false; // 공중에 매달려야 하므로 네비게이션 강제 종료

        // 1. 천장 부착 시도
        _isAttachedToCeiling = TryAttachToCeiling();

        // 부착에 실패했을 때의 상태 전환(ChangeState) 처리는 
        // 상태 머신의 안전한 흐름을 위해 Update()의 첫 프레임으로 위임
    }

    public override void Exit()
    {
        base.Exit();

        // 상태를 벗어날 때 (떨어지거나 순찰로 돌아갈 때) 몬스터의 몸을 다시 똑바로 세워줍니다.
        owner.transform.rotation = Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f);
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱 
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 천장 부착 실패 시 안전하게 순찰 상태로 복귀시킵니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        if (!owner.IsServer) return;

        // 천장이 너무 낮거나 없어서 붙지 못했다면 즉시 순찰로 복귀
        if (!_isAttachedToCeiling)
        {
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }
    }

    /// <summary>
    /// 0.05초마다 실행: 머리 밑으로 지나가는 플레이어가 있는지 레이더를 돌립니다.
    /// </summary>
    protected override void OnTick()
    {
        if (!owner.IsServer || !_isAttachedToCeiling) return;

        CheckForPreyBelow();
    }


    // =========================================================
    // 4. 퍼블릭 함수 
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수
    // =========================================================

    /// <summary>
    /// 머리 위로 레이저를 쏴서 안착할 수 있는 천장인지 확인하고, 가능하면 거꾸로 매달립니다.
    /// </summary>
    /// <returns>천장 부착 성공 여부</returns>
    private bool TryAttachToCeiling()
    {
        // 발사 지점을 가슴 높이(1.5f)로 올려서 바닥의 잡동사니에 맞는 것을 방지
        Vector3 rayStartPos = owner.transform.position + (Vector3.up * 1.5f);

        // 위쪽으로 레이캐스트 발사
        if (Physics.Raycast(rayStartPos, Vector3.up, out RaycastHit hit, data.ceilingCheckDistance, data.ceilingLayerMask))
        {
            float ceilingHeight = hit.point.y - owner.transform.position.y;

            // 천장 높이가 2.5m 이상일 때만 매달림 (너무 낮으면 플레이어가 지나갈 수 없으므로)
            if (ceilingHeight >= 2.5f)
            {
                // 천장에 파묻히지 않도록 약간(0.5f) 아래로 띄워서 위치 고정
                Vector3 ceilingPos = hit.point - new Vector3(0, 0.5f, 0);
                owner.transform.position = ceilingPos;

                // X축을 180도 돌려 거꾸로 매달린 연출
                owner.transform.rotation = Quaternion.Euler(180f, owner.transform.eulerAngles.y, 0f);

                Debug.Log($"<color=yellow>[Snare Flea]</color> 적합한 천장({ceilingHeight:F1}m)에 안착했습니다.");
                return true;
            }
            else
            {
                Debug.Log($"<color=yellow>[Snare Flea]</color> 장애물({ceilingHeight:F1}m)이 너무 낮아 천장으로 부적합합니다.");
                return false;
            }
        }

        Debug.Log("<color=yellow>[Snare Flea]</color> 머리 위에 천장이 없거나 너무 높습니다.");
        return false;
    }

    /// <summary>
    /// 발밑을 지나가는 플레이어가 있는지 검사하고, 발견 시 낙하(AttachedState)를 지시합니다.
    /// </summary>
    private void CheckForPreyBelow()
    {
        foreach (var player in PlayerController.AllPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.isDead.Value) continue;

            Vector3 fleaPos = owner.transform.position;
            Vector3 playerPos = player.transform.position;

            // 1. 수학적 원기둥(Cylinder) 판정: 수평 거리와 수직 높이를 따로 계산
            float horizontalDist = Vector2.Distance(new Vector2(fleaPos.x, fleaPos.z), new Vector2(playerPos.x, playerPos.z));
            float heightDiff = fleaPos.y - playerPos.y;

            // 설정한 반경 안에 있고, 벼룩보다 아래에 있으며, 너무 멀리(바닥 밑 등) 있지 않은지 확인
            if (horizontalDist <= data.dropTriggerRadius && heightDiff > 0 && heightDiff <= data.ceilingCheckDistance)
            {
                // 2. 장애물 검사 (천장에 있는 벼룩과 플레이어 사이에 엄폐물이 있는지 확인)
                // 벼룩 위치보다 약간 아래(Vector3.down * 0.8f)에서 쏴서 천장 자체에 레이저가 막히는 버그 방지
                Vector3 rayStart = fleaPos + (Vector3.down * 0.8f);
                Vector3 playerChestPos = playerPos + (Vector3.up * 1.0f);

                Vector3 dropDir = (playerChestPos - rayStart);
                float dropDist = dropDir.magnitude;

                // [디버그] 씬 뷰에서 벼룩이 누굴 쳐다보는지 붉은 레이저로 확인 가능
                Debug.DrawRay(rayStart, dropDir.normalized * dropDist, Color.red, 1.0f);

                // 중간에 가로막는 장애물(천장/바닥 레이어)이 없다면?
                if (!Physics.Raycast(rayStart, dropDir.normalized, dropDist, data.ceilingLayerMask))
                {
                    Debug.Log($"<color=red>[Snare Flea]</color> {player.name} 포착! 수직 낙하합니다.");

                    // 타겟을 강제로 고정하고 들러붙는 상태로 전이
                    owner.scanner.SetForceTarget(player.transform);
                    owner.ChangeState(MonsterStateType.Attached);
                    return;
                }
                else
                {
                    Debug.Log("<color=gray>[Snare Flea]</color> 플레이어를 감지했으나 장애물에 가려져 낙하할 수 없습니다.");
                }
            }
        }
    }
}