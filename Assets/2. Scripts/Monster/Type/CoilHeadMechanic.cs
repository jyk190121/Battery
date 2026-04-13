using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 코일헤드 전용 기믹 스크립트 (PlayerController 연동 완료)
/// </summary>
[RequireComponent(typeof(MonsterController))]
public class CoilHeadMechanic : NetworkBehaviour
{
    private MonsterController controller;

    //[Header("References")]
    // 플레이어의 메인 카메라나 머리 Transform을 인스펙터에서 넣어줍니다. 안 그러면 플레이어가 마우스를 돌려서 몬스터를 바라봐도 현재는 몸통이 기준이기 때문에 시야처리가 완벽하게 안될 수도 있다.
    //public Transform playerCameraTransform;
    [Header("Coil-Head Settings")]
    [Tooltip("플레이어의 시야각(FOV) 판단 기준 (0.5 = 약 90도)")]
    public float fieldOfViewThreshold = 0.5f;
    [Tooltip("시야가 가려졌는지 판단할 장애물 레이어")]
    public LayerMask obstacleMask;

    private void Awake()
    {
        controller = GetComponent<MonsterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            controller.OnCheckGimmickPause += CheckIfLookedByAnyPlayer;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            controller.OnCheckGimmickPause -= CheckIfLookedByAnyPlayer;
        }
    }

    private bool CheckIfLookedByAnyPlayer()
    {
        Vector3 monsterCenter = transform.position + Vector3.up * 1.5f;

        float maxDistance = controller.monsterData.gimmickCheckDistance;

        foreach (PlayerController player in PlayerController.AllPlayers)
        {
            // 죽은 플레이어나 비활성화된 플레이어는 시야 판정에서 제외
            if (player == null || !player.gameObject.activeInHierarchy || player.IsDead) continue;

            // 임시로 플레이어의 몸통 위치와 정면(Forward)을 시야 기준으로 잡습니다.
            // (나중에 플레이어 카메라나 머리 Transform을 연결해 주시면 더 정교해집니다)
            Vector3 playerEyePos = player.transform.position + Vector3.up * 1.5f;
            Vector3 playerLookDir = player.transform.forward;

            Vector3 dirToMonster = (monsterCenter - playerEyePos).normalized;
            float distanceToMonster = Vector3.Distance(playerEyePos, monsterCenter);

            if (distanceToMonster > maxDistance) continue;

            // 시야각 내에 몬스터가 있는지 확인
            if (Vector3.Dot(playerLookDir, dirToMonster) > fieldOfViewThreshold)
            {
                // 벽에 가려졌는지 레이캐스트 확인
                if (!Physics.Raycast(playerEyePos, dirToMonster, distanceToMonster, obstacleMask))
                {
                    return true; // 살아있는 누군가가 보고 있으므로 정지
                }
            }
        }

        return false; // 아무도 안 봄 ->  돌진
    }
} 