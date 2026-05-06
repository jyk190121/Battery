using UnityEngine;
using Unity.Netcode; // Netcode 네임스페이스 추가

// 1. NetworkBehaviour 상속으로 변경
public class DoorController : NetworkBehaviour
{
    public enum DoorType { Swing, Slide }
    public DoorType doorType;

    [Header("Settings")]
    public NetworkVariable<bool> isOpen = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isLocked = new NetworkVariable<bool>(false);

    public string requiredKeyID;
    public float speed = 3f;

    [Header("Swing Settings")]
    public float openAngle = 90f;

    [Header("Slide Settings")]
    public Vector3 openOffset = new Vector3(1.2f, 0, 0);

    private Vector3 closedPos;
    private Quaternion closedRot;

    public bool CanOpenWithoutKey => !isLocked.Value;

    void Start()
    {
        closedPos = transform.localPosition;
        closedRot = transform.localRotation;
    }

    void Update()
    {
        // 3. isOpen.Value 를 참조하여 애니메이션 처리 (Update는 모든 클라이언트에서 각자 돌아가며 부드럽게 움직임)
        if (doorType == DoorType.Swing)
        {
            Quaternion targetRot = isOpen.Value ? closedRot * Quaternion.Euler(0, openAngle, 0) : closedRot;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * speed);
        }
        else
        {
            Vector3 targetPos = isOpen.Value ? closedPos + openOffset : closedPos;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * speed);
        }
    }

    // 4. 클라이언트가 상호작용할 때 호출하는 함수
    public void TryOpen(string heldKeyID)
    {
        // 문을 열고 닫는 '권한'은 서버에게 맡깁니다.
        if (IsServer)
        {
            ProcessDoorLogic(heldKeyID);
        }
        else
        {
            // 클라이언트라면 서버에게 "문 열어줘" 라고 요청 
            RequestOpenDoorServerRpc(heldKeyID);
        }
    }

    // 클라이언트의 요청을 받아 서버에서 실행되는 함수
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)] 
    private void RequestOpenDoorServerRpc(string heldKeyID)
    {
        ProcessDoorLogic(heldKeyID);
    }

    // 실제 문 열림/잠금 해제 로직 (서버에서만 실행됨)
    private void ProcessDoorLogic(string heldKeyID)
    {
        if (isOpen.Value)
        {
            isOpen.Value = false;
            return;
        }

        if (isLocked.Value)
        {
            if (heldKeyID == requiredKeyID)
            {
                Debug.Log("<color=green>열쇠가 일치합니다! 잠금을 해제하고 문을 엽니다.</color>");
                isLocked.Value = false; // NetworkVariable 값 변경 -> 모든 클라이언트에게 자동 동기화
                isOpen.Value = true;
            }
            else
            {
                Debug.Log("<color=red>문이 잠겨 있습니다. 맞는 열쇠가 필요합니다.</color>");
            }
        }
        else
        {
            isOpen.Value = true;
        }
    }

    [ContextMenu("Force Unlock")] public void ForceUnlock() { if (IsServer) isLocked.Value = false; }
    [ContextMenu("Force Lock")] public void ForceLock() { if (IsServer) isLocked.Value = true; }
}