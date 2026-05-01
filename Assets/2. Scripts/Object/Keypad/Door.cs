using UnityEngine;
using System.Collections; // 코루틴(IEnumerator)을 사용하기 위해 필요합니다.

public class Door : MonoBehaviour
{
    public string password;

    [Header("Door Settings")]
    public float openSpeed = 2f; // 문이 열리는 속도 (인스펙터에서 조절 가능)
    private bool isOpen = false; // 이미 문이 열리고 있는지 체크 (중복 실행 방지)

    public void SetPassword(string newPassword)
    {
        password = newPassword;
    }

    public void OpenDoor()
    {
        // 문이 닫혀있을 때만 열기 코루틴 실행
        if (!isOpen)
        {
            isOpen = true;
            StartCoroutine(OpenDoorRoutine());
        }
    }

    // 천천히 열리게 하는 코루틴
    private IEnumerator OpenDoorRoutine()
    {
        Quaternion startRotation = transform.rotation; // 현재 문의 회전값 (시작점)
        Quaternion targetRotation = Quaternion.Euler(0, -110, 0); // 목표 회전값 (도착점)

        float time = 0f;

        // time이 1이 될 때까지(도착점에 도달할 때까지) 반복
        while (time < 1f)
        {
            // Time.deltaTime을 더해 매 프레임마다 time 값을 증가시킴
            time += Time.deltaTime * openSpeed;

            // Slerp(시작점, 도착점, 0~1사이의 진행도)를 통해 부드러운 회전값 계산
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, time);

            // 다음 프레임까지 대기 (이 줄이 있어야 천천히 열립니다)
            yield return null;
        }

        // 루프가 끝난 후 목표 회전값으로 오차 없이 정확히 맞춰줌
        transform.rotation = targetRotation;
    }

    public void checkPassword(string input)
    {
        if (input == password)
        {
            OpenDoor();
        }
    }
}