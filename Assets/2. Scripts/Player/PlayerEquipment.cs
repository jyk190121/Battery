using Unity.Netcode;
using UnityEngine;
using System.Collections;
using Key = UnityEngine.InputSystem.Key;
using UnityEngine.InputSystem;
using Unity.VisualScripting;

public class PlayerEquipment : NetworkBehaviour
{
    [Header("오브젝트 참조")]
    [Tooltip("캐릭터 오른손에 자식으로 붙어있는 스마트폰 3D 모델")]
    public GameObject smartphoneModel;

    [Tooltip("스마트폰이 생성될 오른손 뼈대(Transform)")]
    public Transform handSocket;

    [Header("현재 장착된 실제 아이템 (무기 등)")]
    [Tooltip("내가 왼쪽손에 들고 있는 아이템 유형")]
    public ItemBase currentEquippedItem;

    [Header("아이템 판별")]
    PlayerInventory _inventory;

    // 현재 무기를 들고 있는지 여부를 외부(PlayerAttack)에서 확인하기 위한 프로퍼티
    public bool HasWeapon { get; private set; }
    public bool IsUsingPhone => isUsingPhone.Value;

    // 현재 생성되어 있는 폰 객체
    GameObject spawnedPhone;

    PlayerAnim playerAnim;
    PlayerAttack _playerAttack;
    GameObject _phoneUIParent;


    // 네트워크 동기화 변수: 모든 클라이언트가 상태를 공유함
    private NetworkVariable<bool> isUsingPhone = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        playerAnim = GetComponent<PlayerAnim>();
        _playerAttack = GetComponent<PlayerAttack>();
        _inventory = GetComponent<PlayerInventory>();

        // 상태 동기화 이벤트 등록
        isUsingPhone.OnValueChanged += OnPhoneStateChanged;

        // 애니메이터 준비를 위해 한 프레임 뒤 실행
        if (IsSpawned)
        {
            StartCoroutine(InitStateAfterFrame());
        }
        // 인벤토리 슬롯이 바뀌거나 아이템이 업데이트될 때 무기 체크를 다시 수행
        if (_inventory != null)
        {
            _inventory.OnSlotChanged += (index) => UpdateWeaponStatus();
            _inventory.OnInventoryUpdated += UpdateWeaponStatus;
        }
    }

    public override void OnNetworkDespawn()
    {
        isUsingPhone.OnValueChanged -= OnPhoneStateChanged;
        if (spawnedPhone != null) Destroy(spawnedPhone);
    }

    void Update()
    {
        if (!IsOwner) return;

        // Q 키 토글
        if (Input.GetKeyDown(Key.Q))
        {
            if (PlayerInventory.IsHoldingTwoHanded)
            {
                Debug.Log("[System] 양손 무기를 들고 있을 때는 폰을 꺼낼 수 없습니다.");
                return;
            }
            isUsingPhone.Value = !isUsingPhone.Value;
        }

        // F키 토글
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            // 폰을 보고 있을 때는 작동안함
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.isPhoneActive) return;

            ItemBase heldItem = (_inventory != null) ? _inventory.HeldItem : null;

            // 현재 손에 들고 있는 아이템이 '손전등(Item_Flash)'일 때만 켜고 끕니다!
            if (heldItem != null && heldItem is Item_Flash)
            {
                heldItem.RequestUseItem(Camera.main.transform.forward);
            }
        }

        // 마우스 좌클릭 통합 관리
        if (Input.GetMouseButtonDown(0))
        {
            // 스마트폰을 보고 있거나 제어가 잠긴 경우 제외
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.isPhoneActive) return;

            ExecutePrimaryAction();
        }

        UpdateAnimationByUIState();
    }
    // 인벤토리 슬롯이 바뀔 때 호출됨
    public void OnSlotItemChanged(ItemBase newItem)
    {
        //currentEquippedItem = newItem;
        //UpdateWeaponStatus();

        //if (_playerAttack == null) _playerAttack = GetComponent<PlayerAttack>();

        // 1. 새로 든 아이템이 무기(Item_Weapon)인지 확인
        if (newItem is Item_Weapon weapon)
        {
            // SO나 무기 스크립트에 설정된 attackPower를 PlayerAttack에 전달
            _playerAttack.SetAttackDamage(weapon.attackPower);
            Debug.Log($"무기 장착: {newItem.itemData.itemName}, 데미지: {weapon.attackPower}");
        }
        else
        {
            // 2. 무기가 아니거나 빈 손이면 데미지 초기화
            _playerAttack.ResetAttackDamage();
        }
    }


    void UpdateAnimationByUIState()
    {
        if (PhoneUIController.Instance != null)
        {
            if (_phoneUIParent == null) _phoneUIParent = PhoneUIController.Instance.phoneUIParent;

            if (_phoneUIParent != null)
            {
                // 실제 UI 게임 오브젝트가 켜져 있는지 확인
                bool isActualShowing = _phoneUIParent.activeInHierarchy;

                // 애니메이션 업데이트 (손을 올리거나 내리는 동작)
                if (playerAnim != null)
                {
                    playerAnim.UpdatePhoneAnimation(isActualShowing);
                }

                // 폰을 집어넣었는데 모델이 남아있다면 제거 (예외 상황 방지)
                if (!isActualShowing && spawnedPhone != null)
                {
                    DestroySmartPhoneModel();
                }
            }
        }
    }

    IEnumerator InitStateAfterFrame()
    {
        yield return null;
        RefreshPhoneState(isUsingPhone.Value);
    }

    // 값의 변화에 따라 실제 비주얼을 업데이트하는 핵심 함수
    void OnPhoneStateChanged(bool previousValue, bool newValue)
    {
        RefreshPhoneState(newValue);
    }

    void RefreshPhoneState(bool isShowing)
    {
        // 애니메이션 및 레이어 업데이트
        if (playerAnim != null)
        {
            // 파라미터와 레이어 무게를 동시에 관리하는 함수 호출
            playerAnim.UpdatePhoneAnimation(isShowing);
        }

        if (!isShowing)
        {
            DestroySmartPhoneModel();
        }

    }

    public void CreateSmartPhoneModel()
    {
        if (spawnedPhone != null) return; // 중복 생성 방지

        if (smartphoneModel != null && handSocket != null)
        {
            spawnedPhone = Instantiate(smartphoneModel, handSocket);
            spawnedPhone.transform.localPosition = new Vector3(-0.002f, 0.062f, -0.002f);
            spawnedPhone.transform.localRotation = Quaternion.identity * Quaternion.Euler(-45f,0,-90f);
        }
    }

    public void DestroySmartPhoneModel()
    {
        if(spawnedPhone != null)
        {
            Destroy(spawnedPhone);
            spawnedPhone = null;
        }
    }

    public void SetEquippedItem(ItemBase item)
    {
        currentEquippedItem = item;
    }

    // 아이템 해제 시
    public void ClearEquippedItem()
    {
        currentEquippedItem = null;
    }

    // 현재 들고 있는 아이템의 카테고리를 확인하여 HasWeapon 갱신
    public void UpdateWeaponStatus()
    {
        if (_inventory != null && _inventory.HeldItem != null)
        {
            // ItemCategory가 Weapon인 경우에만 true
            HasWeapon = _inventory.HeldItem.itemData.category == ItemCategory.Weapon;
        }
        else
        {
            HasWeapon = false;
        }
    }

    // 마우스 좌클릭 시 동작 결정
    public void ExecutePrimaryAction()
    {
        // 인벤토리에서 현재 들고 있는 아이템 확인
        ItemBase item = (_inventory != null) ? _inventory.HeldItem : null;

        if (item == null) return;
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null && !controller.CanUseItem) return;

        // 무기 여부 업데이트
        UpdateWeaponStatus();

        if (HasWeapon)
        {
            // 무기라면 PlayerAttack 스크립트로 전달
            if (_playerAttack != null) _playerAttack.AttemptAttack();
        }
        else if (item.itemData.category == ItemCategory.Consumable)
        {
            Vector3 lookDir = Camera.main.transform.forward;
            int currentIndex = _inventory.currentSlotIndex;

            _inventory.RequestClearSlotServerRpc(currentIndex);

            _inventory.ClearItemReference(item);

            item.RequestUseItem(lookDir);
        }
        else if (item.itemData.category == ItemCategory.Durability)
        {
            // 내구도 아이템(손전등 등)을 좌클릭 했을 때의 행동
            // F키로 켜기로 했으므로, 좌클릭은 아무 일도 일어나지 않게 비워둡니다
            // (만약 나중에 좌클릭으로도 켜고 싶다면 여기에 item.RequestUseItem(lookDir); 한 줄만 넣으면 됩니다)
        }
    }
}