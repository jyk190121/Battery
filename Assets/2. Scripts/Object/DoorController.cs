using UnityEngine;

public class DoorController : MonoBehaviour
{
    public enum DoorType { Swing, Slide }
    public DoorType doorType;

    [Header("Settings")]
    public bool isOpen = false;
    public bool isLocked = false; 
    public string requiredKeyID;     // 필요한 열쇠 ID (예: "Science_Key")
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
    //public void ToggleDoor()
    //{
    //    isOpen = !isOpen;
    //}

    public void TryOpen(string heldKeyID)
    {
        if (isOpen)
        {
            isOpen = false;
            return;
        }

        if (isLocked)
        {
            // 전달받은 KeyID와 필요한 KeyID가 일치하는지 확인
            if (heldKeyID == requiredKeyID)
            {
                Debug.Log("<color=green>열쇠가 일치합니다! 잠금을 해제하고 문을 엽니다.</color>");
                isLocked = false;
                isOpen = true;
            }
            else
            {
                Debug.Log("<color=red>문이 잠겨 있습니다. 맞는 열쇠가 필요합니다.</color>");
            }
        }
        else
        {
            isOpen = true;
        }
    }

    [ContextMenu("Force Unlock")] public void ForceUnlock() { isLocked = false; }
    [ContextMenu("Force Lock")] public void ForceLock() { isLocked = true; }

}
