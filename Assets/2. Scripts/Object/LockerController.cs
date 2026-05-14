using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class LockerController : MonoBehaviour
{
    [Header("Locker Settings")]
    [Tooltip("텔레포트할 도착 지점 (빈 오브젝트)")]
    public Transform destination;    
    [Tooltip("체크하면 밖->안 (Enter), 해제하면 안->밖 (Out)")]
    public bool isHidePoint = true;  

    public string GetInteractText()
    {
        return isHidePoint ? "Hide (E)" : "Exit (E)";
    }

    public void InteractLocker(Transform playerTransform)
    {
        if (destination == null) return;

        if (playerTransform.TryGetComponent<PlayerController>(out var player))
        {
            // 1. 위치 이동 
            var netTransform = playerTransform.GetComponent<NetworkTransform>();
            if (netTransform != null)
            {
                netTransform.Teleport(destination.position, destination.rotation, playerTransform.localScale);
            }
            else
            {
                playerTransform.position = destination.position;
                playerTransform.rotation = destination.rotation;
            }

            // 2. 상태 설정 (Owner인 경우에만 중요 변수 업데이트)
            if (player.IsOwner)
            {
                // 기존 코드: 클라이언트가 억지로 값을 바꾸려 해서 에러 발생
                // player.isInsideFacility.Value = isHidePoint; 

                player.SetInsideFacilityServerRpc(isHidePoint);

                // 락커 안에서는 움직이지 못하게 처리
                // player.GetComponent<PlayerMovement>().enabled = !isHidePoint;
            }

            // 3. 물리 초기화
            if (playerTransform.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
        }
    }
}