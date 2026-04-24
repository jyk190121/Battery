using UnityEngine;
using UnityEngine.InputSystem;
using System; // Action 이벤트 사용을 위해 필요
using Unity.Netcode;
using Unity.VisualScripting;

public enum TVScreenState
{
    MAIN,
    QUEST,
    SHOP_CONSUME,
    SHOP_DURABLE,
    SHOP_STAT,
    SHOP_WEAPON,
}

public class TabletUIManager : NetworkBehaviour
{
    public static TabletUIManager Instance;

    public static event Action<bool> OnTabletStateChanged;

    // TV 화면의 현재 상태를 네트워크 변수로 관리하여 모든 클라이언트가 동기화된 정보를 가질 수 있도록 함
    [Header("Network States")]
    public NetworkVariable<TVScreenState> CurrentTVScreenState =new NetworkVariable<TVScreenState>(
        TVScreenState.MAIN,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Tablet Occupancy")]
    public NetworkVariable<ulong> currentTabletUser = new NetworkVariable<ulong>(
        ulong.MaxValue, // 초기값은 아무도 사용하지 않는 상태를 나타내는 최대값으로 설정
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("UI Panels")]
    public GameObject mainPanel;
    public GameObject questPanel;
    public GameObject shopParentPanel;
    public GameObject[] shopCategoryPanels;

    [Header("Camera & RenderTexture Settings")]
    public Camera uiCamera;
    public RenderTexture tvRenderTexture;

    private Canvas playerHudCanvas;
    private bool isLocalTabletOpen = false;

    [Header("Quest Description Sync")]
    public NetworkVariable<int> hoveredQuestIndex = new NetworkVariable<int>(
        -1, // 초기값은 어떤 퀘스트도 호버링하지 않는 상태를 나타내는 -1로 설정
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Quest Panels")]
    public GameObject[] questDescriptionPanel;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // TV 화면 상태가 변경될 때마다 UI를 업데이트하도록 이벤트 구독
        CurrentTVScreenState.OnValueChanged += (oldValue, newValue) => { UpdateLocalUI(newValue); };

        UpdateLocalUI(CurrentTVScreenState.Value); // 초기 UI 설정

        // 인덱스가 변할 때마다 모든 클라이언트에서 실행
        hoveredQuestIndex.OnValueChanged += (prev, next) => SyncQuestDescription(next);

        // 초기 상태 반영
        SyncQuestDescription(hoveredQuestIndex.Value);
    }

    private void Start()
    {
        LocalCloseProcess();
    }

    private void Update()
    {
        if(isLocalTabletOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTabletUI();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestScreenChangeServerRpc(TVScreenState newMode)
    {
        CurrentTVScreenState.Value = newMode; // 서버에서 상태 변경, 모든 클라이언트에 자동으로 동기화됨
    }

    // 태블릿 UI를 닫을 때 호출되는 로컬 함수 (PlayerController에서 호출)
    public void UpdateLocalUI(TVScreenState state)
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (questPanel != null) questPanel.SetActive(false);
        if (shopParentPanel != null) shopParentPanel.SetActive(false);

        foreach (var panel in shopCategoryPanels)
        {
            if (panel != null) panel.SetActive(false);
        }

        switch(state)
        {
            case TVScreenState.MAIN:
                mainPanel.SetActive(true);
                break;
            case TVScreenState.QUEST:
                questPanel.SetActive(true);
                break;
            case TVScreenState.SHOP_CONSUME:
                if (shopParentPanel != null) shopParentPanel.SetActive(true);
                if (shopCategoryPanels.Length > 0) shopCategoryPanels[0].SetActive(true);
                break;
            case TVScreenState.SHOP_DURABLE:
                if (shopParentPanel != null) shopParentPanel.SetActive(true);
                if (shopCategoryPanels.Length > 1) shopCategoryPanels[1].SetActive(true);
                break;
            case TVScreenState.SHOP_STAT:
                if (shopParentPanel != null) shopParentPanel.SetActive(true);
                if (shopCategoryPanels.Length > 2) shopCategoryPanels[2].SetActive(true);
                break;
            case TVScreenState.SHOP_WEAPON:
                if (shopParentPanel != null) shopParentPanel.SetActive(true);
                if (shopCategoryPanels.Length > 3) shopCategoryPanels[3].SetActive(true);
                break;
        }
    }


    /// <summary>
    /// 태블릿을 열고 조작 모드로 전환 (PlayerInteraction에서 호출)
    /// </summary>
    public void OpenTabletUI(PlayerController player)
    {
        // 필수 방어 코드: 아직 네트워크에 등록되지 않았다면 실행하지 않음
        if (!IsSpawned)
        {
            Debug.LogWarning("[TabletUI] 아직 네트워크에 스폰되지 않아 열 수 없습니다.");
            return;
        }

        if (currentTabletUser.Value != ulong.MaxValue && currentTabletUser.Value != player.OwnerClientId)
        {
            Debug.Log("이미 다른 플레이어가 태블릿을 사용 중입니다.");
            return;
        }

        // 서버에 태블릿 점유 요청을 보내고, 점유가 허용되면 UI를 열도록 합니다.
        RequestTabletOccupancyServerRpc(player.OwnerClientId, true);

        // 로컬에서 태블릿이 열렸음을 표시하고 UI 업데이트
        isLocalTabletOpen = true;
        OnTabletStateChanged?.Invoke(true);

        if (uiCamera != null)
        {
            uiCamera.targetTexture = null;
            uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        }

        if (PlayerUIManager.LocalInstance != null && PlayerUIManager.LocalInstance.playerHpImage != null)
        {
            playerHudCanvas = PlayerUIManager.LocalInstance.playerHpImage.GetComponentInParent<Canvas>();
            if (playerHudCanvas != null) playerHudCanvas.enabled = false;
        }

        if (player.Interaction != null && player.Interaction.interactUI != null)
        {
            player.Interaction.interactUI.SetActive(false);
        }
    }

    /// <summary>
    /// 태블릿을 닫고 다시 TV 송출 모드로 전환
    /// </summary>
    public void CloseTabletUI(PlayerController player)
    {
        if(currentTabletUser.Value == player.OwnerClientId)
        {
            RequestTabletOccupancyServerRpc(player.OwnerClientId, false);
        }
        LocalCloseProcess();
    }

    public void CloseTabletUI()
    {
        if(NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();
            if(player != null && currentTabletUser.Value == player.OwnerClientId)
            {
                RequestTabletOccupancyServerRpc(player.OwnerClientId, false);
            }
        }

        LocalCloseProcess();
    }

    private void LocalCloseProcess()
    {
        if(!isLocalTabletOpen) return;

        isLocalTabletOpen = false;
        OnTabletStateChanged?.Invoke(false);

        if(uiCamera != null)
        {
            uiCamera.targetTexture = tvRenderTexture;
            uiCamera.backgroundColor = Color.black;
        }

        if (playerHudCanvas != null)
        {
            playerHudCanvas.enabled = true;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTabletOccupancyServerRpc(ulong clientId, bool isOccupying)
    {
        if(isOccupying)
        {
            if(currentTabletUser.Value == ulong.MaxValue) // 아무도 사용 중이지 않을 때만 점유 허용
            {
                currentTabletUser.Value = clientId;
                Debug.Log($"플레이어 {clientId}가 태블릿을 점유했습니다.");
            }
            else
            {
                Debug.Log($"플레이어 {clientId}가 태블릿 점유를 시도했지만 이미 사용 중입니다.");
            }
        }
        else
        {
            if(currentTabletUser.Value == clientId) // 점유 해제는 현재 사용자만 가능
            {
                currentTabletUser.Value = ulong.MaxValue;
                CurrentTVScreenState.Value = TVScreenState.MAIN; // 태블릿 닫을 때 항상 메인 화면으로 초기화
                Debug.Log($"플레이어 {clientId}가 태블릿 점유를 해제했습니다.");
            }
        }
    }



    // 퀘스트 설명 패널 동기화 함수
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetHoveredQuestIndexServerRpc(int index)
    {
        hoveredQuestIndex.Value = index;
    }

    private void SyncQuestDescription(int index)
    {
        // 1. 모든 패널을 먼저 끄기 (중복 및 잔상 방지)
        for (int i = 0; i < questDescriptionPanel.Length; i++)
        {
            if (questDescriptionPanel[i] != null)
                questDescriptionPanel[i].SetActive(false);
        }

        // 2. 유효한 인덱스일 때만 해당 패널 활성화 및 데이터 갱신
        if (index >= 0 && index < questDescriptionPanel.Length)
        {
            if (QuestManager.Instance != null && index < QuestManager.Instance.activeQuests.Count)
            {
                GameObject targetPanel = questDescriptionPanel[index];
                if (targetPanel != null)
                {
                    var descriptUI = targetPanel.GetComponent<AcceptedQuestDescriptUI>();
                    if (descriptUI != null)
                    {
                        descriptUI.questIndex = index;
                        descriptUI.SetUp(); // 최신 데이터 강제 주입
                    }
                    targetPanel.SetActive(true);
                }
            }
        }
    }
}