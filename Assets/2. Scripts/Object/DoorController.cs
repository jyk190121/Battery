using UnityEngine;

public class DoorController : MonoBehaviour
{
    public enum DoorType { Swing, Slide }
    public DoorType doorType;

    [Header("Settings")]
    public bool isOpen = false; 
    public float speed = 3f;

    [Header("Swing Settings")]
    public float openAngle = 90f;

    [Header("Slide Settings")]
    public Vector3 openOffset = new Vector3(1.2f, 0, 0); 

    private Vector3 closedPos;
    private Quaternion closedRot;

    void Start()
    {
        closedPos = transform.localPosition;
        closedRot = transform.localRotation;
    }

    void Update()
    {
        if (doorType == DoorType.Swing)
        {
            // 회전 처리
            Quaternion targetRot = isOpen ? closedRot * Quaternion.Euler(0, openAngle, 0) : closedRot;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * speed);
        }
        else
        {
            // 이동 처리
            Vector3 targetPos = isOpen ? closedPos + openOffset : closedPos;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * speed);
        }
    }

    // 상태를 바꾸는 토글
    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }
}
