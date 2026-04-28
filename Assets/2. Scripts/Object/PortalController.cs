using Unity.Netcode.Components;
using UnityEngine;

public class PortalController : MonoBehaviour
{
    [Header("Portal Settings (하나의 문에 목적지 2개 할당)")]
    [Tooltip("밖에서 안으로 들어갈 때 텔레포트할 도착 지점")]
    public Transform insideDestination;

    [Tooltip("안에서 밖으로 나갈 때 텔레포트할 도착 지점")]
    public Transform outsideDestination;

    // UI에 띄울 텍스트 반환 (플레이어의 현재 실내외 상태를 매개변수로 받습니다)
    public string GetInteractText(bool isPlayerInside)
    {
        // 플레이어가 이미 안에 있으면 "Out", 밖에 있으면 "Enter" 출력
        return isPlayerInside ? "Out (E)" : "Enter (E)";
    }

    // 실제 텔레포트 실행
    public void TeleportPlayer(Transform playerTransform)
    {
        if (playerTransform.TryGetComponent<PlayerController>(out var playerController))
        {
            // 1. 플레이어의 현재 상태 확인 (안인가 밖인가?)
            bool isInside = playerController.isInsideFacility.Value;

            // 2. 상태에 맞춰 목적지 자동 결정
            Transform targetDestination = isInside ? outsideDestination : insideDestination;

            if (targetDestination == null)
            {
                Debug.LogWarning("도착 지점(Destination)이 설정되지 않았습니다!");
                return;
            }

            // 3. 상태 토글 (서버로 상태 변경 요청)
            if (playerController.IsOwner)
            {
                // 안에 있었으면 밖(false)으로, 밖에 있었으면 안(true)으로 변경
                playerController.SetInsideFacilityServerRpc(!isInside);
                Debug.Log($"<color=cyan>[공간 이동]</color> 텔레포트 방향: {(isInside ? "밖으로" : "안으로")}");
            }

            // 4. 네트워크 텔레포트 실행
            var netTransform = playerTransform.GetComponent<NetworkTransform>();
            if (netTransform != null)
            {
                netTransform.Teleport(targetDestination.position, targetDestination.rotation, transform.localScale);
            }
            else
            {
                // NetworkTransform이 없을 경우 강제 이동
                playerTransform.position = targetDestination.position;
                playerTransform.rotation = targetDestination.rotation;
            }

            // 5. 텔포 직후 물리 관성 제거 (미끄러짐 방지)
            Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // 물리 엔진 강제 동기화
            Physics.SyncTransforms();
        }
    }
}