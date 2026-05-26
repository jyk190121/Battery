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
        Vector3 monsterCenter = transform.position + (Vector3.up * 1.5f);
        float maxDistance = _controller.monsterData.gimmickCheckDistance;

        foreach (PlayerController player in PlayerController.AllPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.isDead.Value)
                continue;

            Transform playerHead = player.headTransform;

            Vector3 playerEyePos = playerHead.position;
            Vector3 playerLookDir = playerHead.forward; // 상하좌우를 모두 포함하는 정확한 시선

            Vector3 dirToMonster = (monsterCenter - playerEyePos).normalized;
            float distanceToMonster = Vector3.Distance(playerEyePos, monsterCenter);

            if (distanceToMonster > maxDistance)
                continue;

            // 내적 계산 (0.5f면 정면 기준 좌우 상하 60도, 즉 120도 원뿔형 시야각)
            // 모니터 화면과 거의 일치하는 판정이 나옵니다.
            if (Vector3.Dot(playerLookDir, dirToMonster) > fieldOfViewThreshold)
            {
                if (!Physics.Raycast(playerEyePos, dirToMonster, distanceToMonster, obstacleMask))
                {
                    return true;
                }
            }
        }
        return false;
    }
}