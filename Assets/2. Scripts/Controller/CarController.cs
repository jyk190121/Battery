using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class CarController : NetworkBehaviour
{
    [Header("Settings")]
    public float openAngle = -90f;        // 열렸을 때 X축 각도
    public float closeAngle = 0f;        // 닫혔을 때 X축 각도
    public float smoothSpeed = 5f;

    [Header("State")]
    // 네트워크 동기화를 위해 NetworkVariable 사용
    public NetworkVariable<bool> isOpen = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Update()
    {
        // 현재 상태에 따른 목표 회전값 계산
        float targetX = isOpen.Value ? openAngle : closeAngle;
        Quaternion targetRotation = Quaternion.Euler(targetX, 0, 0);

        // 부드럽게 회전
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);

    }

    // 플레이어가 호출할 함수
    public void TryOpen(string playerKeyID)
    {
        // 서버에 상태 변경 요청 (RPC)
        ToggleDoorServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc()
    {
        isOpen.Value = !isOpen.Value;
    }
}