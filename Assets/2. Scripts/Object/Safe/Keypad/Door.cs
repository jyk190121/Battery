using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Door : NetworkBehaviour
{
    public string password;

    [Header("Door Settings")]
    public float openSpeed = 2f;
    private bool isOpen = false;

    public GameObject doorInteraction;
    public void SetPassword(string newPassword)
    {
        password = newPassword;
    }

    public void checkPassword(string input)
    {
        if (input == password)
        {
            // 이름이 바뀐 메서드를 호출합니다.
            RequestOpenDoorRpc();
        }
    }

    // 1. 클라이언트 -> 서버 요청
    // [수정됨] 구버전: [ServerRpc(RequireOwnership = false)]
    // 신버전: 서버로 보냄(SendTo.Server), 누구나 호출 가능(RpcInvokePermission.Everyone)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestOpenDoorRpc()
    {
        // 서버가 요청을 받고 모든 클라이언트에게 명령을 내립니다.
        OpenDoorRpc();
    }

    // 2. 서버 -> 모든 클라이언트 명령
    [Rpc(SendTo.Everyone)]
    private void OpenDoorRpc()
    {
        if (!isOpen)
        {
            isOpen = true;
            gameObject.layer = 0;
            StartCoroutine(OpenDoorRoutine());
        }
    }

    // 천천히 열리게 하는 코루틴 (내용 동일)s
    private IEnumerator OpenDoorRoutine()
    {
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(0, -110, 0);

        float time = 0f;

        while (time < 1f)
        {
            time += Time.deltaTime * openSpeed;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, time);
            yield return null;
        }

        transform.rotation = targetRotation;
        doorInteraction.gameObject.layer = 0;
    }
}