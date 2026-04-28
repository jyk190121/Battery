using Unity.Cinemachine;
using Unity.Netcode;
public class FaceMonsterRegister : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // 서버/클라이언트 모두에서 실행하여 각자의 로컬 카메라 컨트롤러에 등록
        CinemachineController controller = FindAnyObjectByType<CinemachineController>();

        if (controller != null)
        {
            CinemachineCamera myVcam = GetComponentInChildren<CinemachineCamera>();
            if (myVcam != null)
            {
                controller.RegisterMonsterCamera(myVcam);
            }
        }
    }
}
