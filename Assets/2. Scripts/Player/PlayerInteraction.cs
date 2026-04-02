using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerInteraction : MonoBehaviour
{
    [Header("Data & Settings")]
    public Player data;                                 // 플레이어 SO
    public LayerMask DoorLayer;                         // 문 레이어
    public GameObject interactUI;                       // UI오브젝트
    public TMPro.TextMeshProUGUI interactText;          // 텍스트

    [Header("References")]
    [SerializeField] private Transform camTransform;    // 카메라 위치

    private bool isLookingAtInteractable = false;       // 문을 보고 있는가

    void Start()
    {
        if (camTransform == null) camTransform = Camera.main.transform;
    }

    // Update is called once per frame
    void Update()
    {
        CheckInteraction();

        if (Keyboard.current.eKey.wasPressedThisFrame == true)
        {
            PerformInteraction();
        }
    }

    public void CheckInteraction()
    {
        RaycastHit hit;

        if (Physics.Raycast(camTransform.position, camTransform.forward, out hit, data.interactDistance, DoorLayer))
        {
            if (!isLookingAtInteractable) interactUI.SetActive(true);
            isLookingAtInteractable = true;

            // 문 상태 확인해서 텍스트 변경
            var door = hit.collider.GetComponentInParent<DoorController>();
            if (door != null)
            {
                interactText.text = door.isOpen ? "Close (E)" : "Open (E)";
            }
        }
        else
        {
            if (isLookingAtInteractable) interactUI.SetActive(false);
            isLookingAtInteractable = false;
        }
    }

    private void PerformInteraction()
    {
        RaycastHit hit;
        if (Physics.Raycast(camTransform.position, camTransform.forward, out hit, data.interactDistance, DoorLayer))
        {
            var door = hit.collider.GetComponentInParent<DoorController>();
            if (door != null)
            {
                // 멀티플레이어: 여기서 서버 RPC 함수를 호출
                door.ToggleDoor();
            }
        }
    }
}
