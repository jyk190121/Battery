using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 몬스터의 핵심 기믹(누군가 쳐다보면 그 자리에 얼어붙음)을 담당하는 스크립트
/// </summary>
[RequireComponent(typeof(MonsterController))]
public class CoilHeadMechanic : NetworkBehaviour
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    [Header("--- Coil-Head Settings ---")]
    [Tooltip("플레이어의 시야각(FOV) 판단 기준 (0.5 = 정면 기준 약 90도 범위 내)")]
    public float fieldOfViewThreshold = 0.5f;

    [Tooltip("플레이어와 몬스터 사이의 시야를 가리는 장애물 레이어")]
    public LayerMask obstacleMask;

    private MonsterController _controller;


    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    private void Awake()
    {
        _controller = GetComponent<MonsterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _controller.gimmickPauseChecks.Add(CheckIfLookedByAnyPlayer);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            _controller.gimmickPauseChecks.Remove(CheckIfLookedByAnyPlayer);
        }
    }


    // =========================================================
    // 3. 유니티 루프 - 본 스크립트에서는 미사용
    // =========================================================


    // =========================================================
    // 4. 퍼블릭 함수 - 본 스크립트에서는 미사용
    // =========================================================


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 현재 맵에 살아있는 플레이어 중 단 한 명이라도 이 몬스터를 쳐다보고 있는지 검사합니다.
    /// </summary>
    /// <returns>누군가 보고 있으면 true (정지), 아무도 안 보면 false (돌진)</returns>
    private bool CheckIfLookedByAnyPlayer()
    {
        // 1. 몬스터의 중심점 계산 (발바닥 대신 가슴/머리 높이 기준)
        Vector3 monsterCenter = transform.position + (Vector3.up * 1.5f);
        float maxDistance = _controller.monsterData.gimmickCheckDistance;

        // 2. 모든 플레이어를 순회하며 시야 검사
        foreach (PlayerController player in PlayerController.AllPlayers)
        {
            // 죽은 플레이어나 비활성화된 플레이어는 시야 판정에서 제외
            if (player == null || !player.gameObject.activeInHierarchy || player.isDead.Value)
                continue;

            // [TODO] 임시 처리: 추후 플레이어의 진짜 카메라(Camera.main)나 머리 Transform으로 교체 권장
            Vector3 playerEyePos = player.transform.position + (Vector3.up * 1.5f);
            Vector3 playerLookDir = player.transform.forward;

            Vector3 dirToMonster = (monsterCenter - playerEyePos).normalized;
            float distanceToMonster = Vector3.Distance(playerEyePos, monsterCenter);

            // 거리가 너무 멀면 쳐다봐도 무효 처리
            if (distanceToMonster > maxDistance)
                continue;

            // 3. 시야각(FOV) 검사 (Dot Product 내적 활용)
            if (Vector3.Dot(playerLookDir, dirToMonster) > fieldOfViewThreshold)
            {
                // 4. 장애물(벽) 가림 검사 (Raycast)
                // 눈에서 몬스터를 향해 레이저를 쏴서 벽에 막히지 않았다면?
                if (!Physics.Raycast(playerEyePos, dirToMonster, distanceToMonster, obstacleMask))
                {
                    return true; // 살아있는 누군가가 확실히 보고 있으므로 몬스터 얼어붙음
                }
            }
        }

        // 모든 검사를 통과했다면 아무도 몬스터를 보지 못하고 있는 상태
        return false;
    }
}