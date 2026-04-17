using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


public class PlayerInteraction : NetworkBehaviour
{
    [Header("Data & Settings")]
    //public Player data;                                 // 플레이어 SO
    PlayerController controller;
    Player data => controller.Data;
    public LayerMask DoorLayer;                         // 문 레이어
    public LayerMask TabletLayer;                       // 태블릿 레이어
    public GameObject interactUI;                       // UI오브젝트
    TextMeshProUGUI interactText;                       // 텍스트
    public Image progressImage;
    public float requiredHoldTime = 2f;
    private float currentHoldTime = 0f;

    //[Header("References")]
    //[SerializeField] private Transform camTransform;    // 카메라 위치

    //씨네머신의 위치값만
    PlayerRotation playerRotation;
    Transform camTransform;

    private bool isLookingAtInteractable = false;       // 문을 보고 있는가

    private DoorController targetDoor = null;
    private PortalController targetPortal = null;
    private TabletUIManager targetTabletUI = null;
    CarController carDoor = null;

    public override void OnNetworkSpawn()
    {
        //interactUI = GameObject.Find("Interact_Text").gameObject;
        ////if (camTransform == null) camTransform = FindAnyObjectByType<CinemachineCamera>().GetComponent<Transform>();
        //if (playerRotation == null) playerRotation = GetComponent<PlayerRotation>();

        ////if (camTransform == null) camTransform = Camera.main.transform;
        //interactText = interactUI.GetComponent<TextMeshProUGUI>();

        //interactUI.SetActive(false);
        controller = GetComponent<PlayerController>();
        if (IsOwner)
        {
            FindUIElements(); // 1. 처음 스폰될 때 한 번 찾기

            // 2. 씬이 바뀔 때마다 'FindUIElements' 함수를 실행하도록 예약(구독)
            //SceneManager.sceneLoaded += OnSceneLoaded;

            if (playerRotation == null) playerRotation = GetComponent<PlayerRotation>();
        }
    }

    //public override void OnNetworkDespawn()
    //{
    //    if (IsOwner)
    //    {
    //        SceneManager.sceneLoaded -= OnSceneLoaded;
    //    }
    //}

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

        if (controller.isDead.Value || controller.isSnared.Value)
        {
            if (isLookingAtInteractable)
            {
                interactUI.SetActive(false);
                isLookingAtInteractable = false;
                ResetHold();
            }
            return; 
        }

        if (isLookingAtInteractable)
        {
            // 1. 포탈인 경우: '홀드(Hold)' 방식
            if (targetPortal != null)
            {
                // E키를 누르고 있는 중인가?
                if (Keyboard.current.eKey.isPressed)
                {
                    // 시간 누적 및 게이지 UI 업데이트
                    currentHoldTime += Time.deltaTime;
                    if (progressImage != null) progressImage.fillAmount = currentHoldTime / requiredHoldTime;

                    // 지정된 시간이 다 차면 텔레포트 실행!
                    if (currentHoldTime >= requiredHoldTime)
                    {
                        targetPortal.TeleportPlayer(this.transform);
                        ResetHold(); // 텔레포트 후 게이지 초기화
                    }
                }
                else
                {
                    // 손을 떼면 즉시 초기화
                    ResetHold();
                }
            }
            // 2. 일반 문인 경우: '클릭(Press)' 방식
            else if (targetDoor != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    string testKeyID = "Test_01";
                    targetDoor.TryOpen(testKeyID);
                }
            }
            else if(targetTabletUI != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    targetTabletUI.OpenTabletUI();
                }
            }
            else if (carDoor != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    string currentKey = "Truck_Zil130_TrailerDoor";

                    carDoor.TryOpen(currentKey);
                }
            }
        }
        else
        {
            // 상호작용 물체에서 시선을 돌리면 무조건 초기화
            ResetHold();
        }
    }

    //private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    //{
    //    FindUIElements();
    //}

    public void CheckInteraction()
    {
        if (playerRotation == null || playerRotation.vcam == null) return;
        camTransform = playerRotation.vcam.transform;

        RaycastHit hit;

        LayerMask combinedLayer = DoorLayer | TabletLayer;

        if (Physics.Raycast(camTransform.position, camTransform.forward, out hit, data.interactDistance, combinedLayer))
        {
            // 타겟 갱신
            targetDoor = hit.collider.GetComponentInParent<DoorController>();
            targetPortal = hit.collider.GetComponentInParent<PortalController>();
            carDoor = hit.collider.GetComponentInParent<CarController>();

            targetTabletUI = hit.collider.GetComponentInChildren<TabletUIManager>();
            if (targetTabletUI == null) targetTabletUI = hit.collider.GetComponentInParent<TabletUIManager>();

            if (targetDoor != null || targetPortal != null || targetTabletUI != null || carDoor != null)
            {
                if (!isLookingAtInteractable) interactUI.SetActive(true);
                isLookingAtInteractable = true;

                if (targetDoor != null)
                {
                    interactText.text = (targetDoor.isLocked && !targetDoor.isOpen) ? "Locked (E)" : (targetDoor.isOpen ? "Close (E)" : "Open (E)");
                }
                else if (targetPortal != null)
                {
                    interactText.text = targetPortal.GetInteractText();
                }
                else if (targetTabletUI != null)
                {
                    interactText.text = "Use Tablet (E)";
                }
                else if (carDoor != null)
                {
                    interactText.text = carDoor.isOpen.Value ? "Close (E)" : "Open (E)";
                }
                return; // 찾았으므로 종료
            }
        }  

        // 아무것도 보지 않을 때
        if (isLookingAtInteractable) interactUI.SetActive(false);
        isLookingAtInteractable = false;
        targetDoor = null;
        targetPortal = null;
        targetTabletUI = null;
        carDoor = null;
    }

    public void FindUIElements()
    {
        GameObject foundUI = GameObject.Find("Interact_Text");
        GameObject foundRing = GameObject.Find("ProgressRing_Img");

        if (foundUI != null)
        {
            interactUI = foundUI;
            interactText = interactUI.GetComponent<TextMeshProUGUI>();
            interactUI.SetActive(false);
        }

        if (foundRing != null)
        {
            progressImage = foundRing.GetComponent<Image>();
            progressImage.fillAmount = 0f;
        }

        if (foundUI == null || foundRing == null)
        {
            string sceneName = (GameSceneManager.Instance != null) ? GameSceneManager.Instance.SceneName() : "Unknown Scene";
        }
    }

    private void ResetHold()
    {
        currentHoldTime = 0f;
        if (progressImage != null) progressImage.fillAmount = 0f;
    }
}
