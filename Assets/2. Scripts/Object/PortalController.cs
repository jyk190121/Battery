using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// [수정] NetworkBehaviour 상속으로 변경 (RPC 사용을 위해)
public class PortalController : NetworkBehaviour
{
    [Header("Portal Settings")]
    public Transform insideDestination;
    public Transform outsideDestination;

    [Header("Sound Settings")]
    public AudioSource portalAudioSource; // 포탈에 부착된 오디오 소스 (3D)
    public AudioClip teleportSound;       // 텔레포트 공통 소리 (또는 Enter/Exit 분리 가능)

    public string GetInteractText(bool isPlayerInside)
    {
        return isPlayerInside ? "Out (E)" : "Enter (E)";
    }

    public void TeleportPlayer(Transform playerTransform)
    {
        if (playerTransform.TryGetComponent<PlayerController>(out var playerController))
        {
            bool isInside = playerController.isInsideFacility.Value;
            Transform targetDestination = isInside ? outsideDestination : insideDestination;

            if (targetDestination == null) return;

            // 1. [사운드 실행] 모든 클라이언트에게 이 위치에서 소리를 재생하라고 명령
            PlayTeleportSoundClientRpc();

            // 2. 상태 토글 및 텔레포트 로직 (기존과 동일)
            if (playerController.IsOwner)
            {
                playerController.SetInsideFacilityServerRpc(!isInside);
            }

            var netTransform = playerTransform.GetComponent<NetworkTransform>();
            if (netTransform != null)
            {
                netTransform.Teleport(targetDestination.position, targetDestination.rotation, transform.localScale);
            }
            else
            {
                playerTransform.position = targetDestination.position;
                playerTransform.rotation = targetDestination.rotation;
            }

            Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
        }
    }

    // 모든 클라이언트에서 포탈 소리가 들리게 하는 RPC
    [ClientRpc]
    private void PlayTeleportSoundClientRpc()
    {
        if (portalAudioSource != null && teleportSound != null)
        {
            // 포탈 위치에서 3D 사운드로 한 번 재생
            portalAudioSource.PlayOneShot(teleportSound);
        }
    }
}