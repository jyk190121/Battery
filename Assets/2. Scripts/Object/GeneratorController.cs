using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GeneratorController : NetworkBehaviour
{
    [Header("Settings")]
    public float repairTime = 3f; // 수리에 필요한 시간
    public NetworkVariable<bool> isRepaired = new NetworkVariable<bool>(false);

    [Header("References")]
    public Transform leverTransform;                                 // 움직일 레버 오브젝트
    public Vector3 leverTargetRotation = new Vector3(-45f, 0, 0);    // 레버가 꺾일 각도
    public List<DoorController> linkableDoors;                       // 이 발전기로 열 수 있는 문 후보들

    private Vector3 initialLeverRotation;

    private void Start()
    {
        initialLeverRotation = leverTransform.localEulerAngles;
    }

    private void Update()
    {
        // 수리가 완료되었다면 레버를 목표 각도로 부드럽게 회전
        if (isRepaired.Value)
        {
            leverTransform.localRotation = Quaternion.Slerp(leverTransform.localRotation,
                Quaternion.Euler(leverTargetRotation), Time.deltaTime * 5f);
        }
    }

    public string GetInteractText()
    {
        if (isRepaired.Value) return "Generator Repaired";
        return "Repair Generator (Hold E)";
    }

    // 서버에서만 실행되는 수리 완료 로직
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CompleteRepairServerRpc()
    {
        if (isRepaired.Value) return; 

        isRepaired.Value = true;
        Debug.Log("<color=yellow>[Generator]</color> 수리 완료 랜덤 문 잠금 해제를 시도.");

        UnlockRandomLockedDoor();
    }

    private void UnlockRandomLockedDoor()
    {
        if (linkableDoors == null || linkableDoors.Count == 0) return;

        // 1. 현재 잠겨있는 문들만 리스트로 추출
        List<DoorController> lockedDoors = linkableDoors.FindAll(d => d.isLocked.Value);

        if (lockedDoors.Count > 0)
        {
            // 2. 그 중 랜덤하게 하나 선택
            int randomIndex = Random.Range(0, lockedDoors.Count);
            DoorController targetDoor = lockedDoors[randomIndex];

            // 3. 문 잠금 해제 (NetworkVariable이므로 모든 클라이언트 동기화됨)
            targetDoor.isLocked.Value = false;
            // 필요하다면 즉시 문을 열게 할 수도 있음
            targetDoor.isOpen.Value = true;

            Debug.Log($"<color=green>[Generator]</color> {targetDoor.gameObject.name}의 잠금이 해제되었습니다.");
        }
        else
        {
            Debug.Log("<color=red>[Generator]</color> 잠긴 문이 없어 아무 일도 일어나지 않았습니다.");
        }
    }
}