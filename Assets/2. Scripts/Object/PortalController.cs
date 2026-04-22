using Unity.Netcode.Components;
using UnityEngine;

public class PortalController : MonoBehaviour
{
    [Header("Portal Settings")]
    [Tooltip("텔레포트할 도착 지점 (빈 오브젝트)")]
    public Transform destination; 
    
    [Tooltip("체크하면 밖->안 (Enter), 해제하면 안->밖 (Out)")]
    public bool isEntrance = true; 

    // UI에 띄울 텍스트 반환
    public string GetInteractText()
    {
        return isEntrance ? "Enter (E)" : "Out (E)";
    }

    // 실제 텔레포트 실행
    public void TeleportPlayer(Transform playerTransform)
    {
        if (destination == null)
        {
            Debug.LogWarning("도착 지점(destination)이 설정되지 않았습니다");
            return;
        }

        if (playerTransform.TryGetComponent<PlayerController>(out var playerController))
        {
            // 이 코드를 실행하는 사람이 자기 자신(Owner)일 때만 네트워크 변수를 수정
            // (isInsideFacility의 Write 권한이 Owner로 설정되어 있기 때문)
            if (playerController.IsOwner)
            {
                //playerController.isInsideFacility.Value = this.isEntrance;
                playerController.SetInsideFacilityServerRpc(this.isEntrance);
                Debug.Log($"<color=cyan>[공간 이동]</color> 플레이어 실내 진입 상태: {playerController.isInsideFacility.Value}");
            }
        }

        var netTransform = playerTransform.GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.Teleport(destination.position, destination.rotation, transform.localScale);
        }

        // 플레이어 위치와 회전값을 도착 지점으로 즉시 변경
        else
        {
            playerTransform.position = destination.position;
            playerTransform.rotation = destination.rotation;
        }

        // 텔포 직후 미끄러짐 방지
        Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;  
            rb.angularVelocity = Vector3.zero;
        }

        // 물리 엔진 강제 동기화 (간혹 위치가 안 갱신되는 유니티 버그 방지)
        Physics.SyncTransforms();

        Debug.Log($"텔레포트 완료: {GetInteractText()}");
    }
}