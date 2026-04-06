using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Cinemachine;


public class PlayerInteraction : NetworkBehaviour
{
    [Header("Data & Settings")]
    public Player data;                                 // 플레이어 SO
    public LayerMask DoorLayer;                         // 문 레이어
    public GameObject interactUI;                       // UI오브젝트
    TextMeshProUGUI interactText;                       // 텍스트

    //[Header("References")]
    //[SerializeField] private Transform camTransform;    // 카메라 위치

    //씨네머신의 위치값만
    PlayerRotation playerRotation;
    Transform camTransform;

    private bool isLookingAtInteractable = false;       // 문을 보고 있는가

    public override void OnNetworkSpawn()
    {
        //interactUI = GameObject.Find("Interact_Text").gameObject;
        ////if (camTransform == null) camTransform = FindAnyObjectByType<CinemachineCamera>().GetComponent<Transform>();
        //if (playerRotation == null) playerRotation = GetComponent<PlayerRotation>();

        ////if (camTransform == null) camTransform = Camera.main.transform;
        //interactText = interactUI.GetComponent<TextMeshProUGUI>();

        //interactUI.SetActive(false);

        if (IsOwner)
        {
            GameObject foundUI = GameObject.Find("Interact_Text");

            if (foundUI != null)
            {
                interactUI = foundUI;
                interactText = interactUI.GetComponent<TextMeshProUGUI>();
                interactUI.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[PlayerInteraction] 'Interact_Text'를 찾을 수 없습니다. UI가 씬에 있는지 확인하세요.");
            }

            if (playerRotation == null) playerRotation = GetComponent<PlayerRotation>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        //CheckInteraction();

        //if (Keyboard.current.eKey.wasPressedThisFrame == true)
        //{
        //    PerformInteraction();
        //}

        if (!IsOwner || interactUI == null) return;

        CheckInteraction();

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            PerformInteraction();
        }
    }

    public void CheckInteraction()
    {
        if (playerRotation == null || playerRotation.vcam == null) return;
        camTransform = playerRotation.vcam.transform;

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
        if (playerRotation == null || playerRotation.vcam == null) return;
        camTransform = playerRotation.vcam.transform;

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
