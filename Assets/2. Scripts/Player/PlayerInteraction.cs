using NUnit;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Data & Settings")]
    PlayerController controller;
    Player data => controller.Data;

    [Header("Interaction Layers")]
    public LayerMask DoorLayer;                 // 문 레이어
    public LayerMask TabletLayer;               // 태블릿 레이어
    public LayerMask Interactable;              // 발전기, 락커 등 레이어

    [Header("Obstacle Settings")]
    [Tooltip("락커 껍데기나 벽처럼 시야를 가리는 레이어 (보통 Default 선택)")]
    public LayerMask obstacleLayer;             // 투시 방지용 방해물 레이어

    [Header("UI References")]
    public GameObject interactUI;               // UI오브젝트
    TextMeshProUGUI interactText;               // 텍스트
    public Image progressImage;

    public float requiredHoldTime = 2f;
    private float currentHoldTime = 0f;

    PlayerRotation playerRotation;
    Transform camTransform;

    private bool isLookingAtInteractable = false;       // 무언가를 보고 있는가

    private DoorController targetDoor = null;
    private PortalController targetPortal = null;
    private TabletUIManager targetTabletUI = null;
    private LockerController targetLocker = null;
    private GeneratorController targetGenerator = null;
    private QuestGeneratorAdapter targetQuestGenerator = null;
    CarController carDoor = null;

    public override void OnNetworkSpawn()
    {
        controller = GetComponent<PlayerController>();
        if (IsOwner)
        {
            FindUIElements();
            if (playerRotation == null) playerRotation = GetComponent<PlayerRotation>();
        }
    }

    void Update()
    {
        if (!IsOwner || interactUI == null) return;

        CheckInteraction();

        // 1. 상태 이상 시 강제 초기화
        if (controller.isDead.Value || controller.isSnared.Value)
        {
            ClearInteraction();
            return;
        }

        if (isLookingAtInteractable)
        {
            // [포탈 홀드 로직]
            if (targetPortal != null)
            {
                if (Keyboard.current.eKey.isPressed)
                {
                    currentHoldTime += Time.deltaTime;
                    if (progressImage != null) progressImage.fillAmount = currentHoldTime / requiredHoldTime;

                    if (currentHoldTime >= requiredHoldTime)
                    {
                        targetPortal.TeleportPlayer(this.transform);
                        ResetHold();
                    }
                }
                else
                {
                    ResetHold(); // 손을 떼면 게이지 초기화
                }
            }
            // [락커 클릭]
            else if (targetLocker != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    targetLocker.InteractLocker(this.transform);
                }
            }
            // [일반 문 클릭]
            else if (targetDoor != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    targetDoor.TryOpen();
                }
            }
            // [태블릿 클릭]
            else if (targetTabletUI != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    targetTabletUI.OpenTabletUI(controller);
                }
            }
            // [트럭 문 클릭]
            else if (carDoor != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    string currentKey = "Truck_Zil130_TrailerDoor";
                    carDoor.TryOpen(currentKey);
                }
            }
            // [발전기 상호작용]
            else if (targetGenerator != null && !targetGenerator.isRepaired.Value)
            {
                // 퀘스트 발전기 (부품 삽입 - 클릭)
                if (targetQuestGenerator != null && targetQuestGenerator.isQuestTarget.Value && targetQuestGenerator.currentParts.Value < targetQuestGenerator.requiredParts.Value)
                {
                    ResetHold();

                    if (Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        if (TryGetComponent(out PlayerInventory playerInv))
                        {
                            targetQuestGenerator.Interact(playerInv);
                        }
                    }
                }
                // 일반 발전기 (수리 - 홀드)
                else
                {
                    if (Keyboard.current.eKey.isPressed)
                    {
                        currentHoldTime += Time.deltaTime;
                        if (progressImage != null) progressImage.fillAmount = currentHoldTime / targetGenerator.repairTime;

                        if (currentHoldTime >= targetGenerator.repairTime)
                        {
                            targetGenerator.CompleteRepairServerRpc();
                            ResetHold();
                        }
                    }
                    else
                    {
                        // [핵심] E키에서 손을 떼면 여기서 즉시 0으로 초기화됩니다!
                        ResetHold();
                    }
                }
            }
            else
            {
                ResetHold();
            }
        }
    }

    public void CheckInteraction()
    {
        if (playerRotation == null || playerRotation.vcam == null) return;
        camTransform = playerRotation.vcam.transform;

        RaycastHit hit;
        LayerMask combinedLayer = DoorLayer | TabletLayer | Interactable;

        if (Physics.Raycast(camTransform.position, camTransform.forward, out hit, data.interactDistance, combinedLayer))
        {
 
            Vector3 checkEndPos = hit.point - (camTransform.forward * 0.05f);
            if (Physics.Linecast(camTransform.position, checkEndPos, obstacleLayer))
            {
                // 락커 밖에서 안에 있는 걸 쳐다봤다면 철벽에 막힌 것으로 간주하고 초기화
                ClearInteraction();
                return;
            }

            // 타겟 갱신
            targetDoor = hit.collider.GetComponentInParent<DoorController>();
            targetPortal = hit.collider.GetComponentInParent<PortalController>();
            carDoor = hit.collider.GetComponentInParent<CarController>();
            targetLocker = hit.collider.GetComponentInParent<LockerController>();
            targetGenerator = hit.collider.GetComponentInParent<GeneratorController>();

            targetTabletUI = hit.collider.GetComponentInChildren<TabletUIManager>();
            if (targetTabletUI == null) targetTabletUI = hit.collider.GetComponentInParent<TabletUIManager>();

            if (targetDoor != null || targetPortal != null || targetTabletUI != null || carDoor != null || targetLocker != null || targetGenerator != null)
            {
                if (!isLookingAtInteractable) interactUI.SetActive(true);
                isLookingAtInteractable = true;

                if (targetDoor != null)
                {
                    interactText.text = (targetDoor.isLocked.Value && !targetDoor.isOpen.Value) ? "Locked (E)" : (targetDoor.isOpen.Value ? "Close (E)" : "Open (E)");
                }
                else if (targetPortal != null)
                {
                    interactText.text = targetPortal.GetInteractText(controller.isInsideFacility.Value);
                }
                else if (targetLocker != null)
                {
                    interactText.text = targetLocker.GetInteractText();
                }
                else if (targetTabletUI != null)
                {
                    interactText.text = "Use Tablet (E)";
                }
                else if (carDoor != null)
                {
                    interactText.text = carDoor.isOpen.Value ? "Close (E)" : "Open (E)";
                }
                else if (targetGenerator != null)
                {
                    targetQuestGenerator = hit.collider.GetComponent<QuestGeneratorAdapter>();
                    if (targetQuestGenerator == null)
                        targetQuestGenerator = hit.collider.GetComponentInParent<QuestGeneratorAdapter>();

                    if (targetQuestGenerator != null && targetQuestGenerator.isQuestTarget.Value)
                    {
                        interactText.text = targetQuestGenerator.GetInteractText();
                    }
                    else
                    {
                        interactText.text = targetGenerator.GetInteractText();
                    }
                }
                return; // 정상적으로 찾았으므로 종료
            }
        }

        // 아무것도 보지 않거나 방해물에 막혔을 때 완벽 초기화
        ClearInteraction();
    }

    // 시선을 돌리거나 투시가 막혔을 때 타겟들을 깔끔하게 날려주는 함수입니다.
    private void ClearInteraction()
    {
        if (isLookingAtInteractable) interactUI.SetActive(false);
        isLookingAtInteractable = false;

        targetDoor = null;
        targetPortal = null;
        targetTabletUI = null;
        targetLocker = null;
        targetGenerator = null;
        targetQuestGenerator = null;
        carDoor = null;

        ResetHold();
    }

    private void ResetHold()
    {
        currentHoldTime = 0f;
        if (progressImage != null) progressImage.fillAmount = 0f;
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
    }
}