using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class Item_Flash : ItemBase
{
    [Header("Flashlight Settings")]
    [Tooltip("손전등 불빛을 쏠 Light 컴포넌트")]
    public Light spotLight;

    [Tooltip("최대 배터리량 (내구도)")]
    public float maxBattery = 100f;

    [Tooltip("초당 배터리 소모량")]
    public float batteryDrainRate = 2f;

    PlayerRotation playerRotation;
    private NetworkTransform netTransform; // ?? 내 오브젝트의 NetworkTransform 컴포넌트 참조

    public NetworkVariable<float> currentBatteryNet = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    protected override void Awake()
    {
        base.Awake();

        if (spotLight != null) spotLight.enabled = false;
        TryGetComponent(out netTransform);
    }
    void OnEnable()
    {
        if (isEquipped && netTransform != null)
        {
            netTransform.enabled = false;
        }
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 1. 서버(방장)라면 SO에 설정된 초기 배터리 값을 동기화 변수에 넣어줍니다.
        if (IsServer)
        {
            currentBatteryNet.Value = maxBattery;
        }

        // 2. [팁] 멀티플레이어 게임에서 초기 불빛 상태 동기화를 위해
        // F키를 안 눌러도 시작 시 불빛 컴포넌트를 켜두는 게 좋습니다. (세이브 로드 대응)
        // 여기서는 복잡도를 낮추기 위해 일단 꺼두는 걸로 유지합니다.
    }

    // ==========================================
    // 1. 아이템 사용 (F키 입력 시 호출됨)
    // ==========================================
    public override void ExecuteUseItem(Vector3 direction)
    {
        base.ExecuteUseItem(direction);

        // 네트워크 변수의 Value를 체크합니다. 배터리가 없으면 안 켜짐.
        if (currentBatteryNet.Value <= 0f)
        {
            // [팁] 배터리 없을 때 딸깍딸깍하는 소리 재생
            return;
        }

        if (spotLight != null)
        {
            spotLight.enabled = !spotLight.enabled;
            // [TODO] 딸깍! 하는 스위치 사운드 재생
        }
    }

    /// <summary>
    /// 아이템을 줍거나 버릴 때 (ItemBase의 함수 오버라이드)
    /// </summary>
    public override void ExecuteChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        base.ExecuteChangeOwnership(isPickingUp, targetHand);

        if (isPickingUp && targetHand != null)
        {
            // 1. 아이템을 주웠을 때: 들고 있는 플레이어의 회전 스크립트를 캐싱합니다.
            playerRotation = targetHand.GetComponentInParent<PlayerRotation>();

            if (netTransform != null) netTransform.enabled = false;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            playerRotation = null;

            if (netTransform != null) netTransform.enabled = true;

            if (spotLight != null)
            {
                spotLight.transform.localRotation = Quaternion.identity;
                spotLight.transform.localPosition = Vector3.zero; // 원래 위치로 복귀
            }
        }
    }

    // ==========================================
    // 2. 배터리 소모 (내구도 계산)
    // ==========================================
    private void Update()
    {
        if (spotLight == null || !spotLight.enabled) return;

        // 주인(Owner)만 배터리를 계산해서 동기화 변수에 씁니다.
        if (IsOwner && isEquipped)
        {
            // 네트워크 변수의 값을 직접 깎습니다.
            float newValue = currentBatteryNet.Value - (batteryDrainRate * Time.deltaTime);
            currentBatteryNet.Value = newValue;

            // [TODO] UI에 현재 배터리량(currentBatteryNet.Value / maxBattery) 업데이트

            if (currentBatteryNet.Value <= 0f)
            {
                currentBatteryNet.Value = 0f;

                // 내 화면에서 먼저 불을 꺼서 Update문 상단의 return; 에 걸리게 합니다.
                spotLight.enabled = false;

                ForceTurnOff();
            }
        }
        //// 누군가 장착 중이고 시선 컴포넌트와 라이트가 유효할 때
        //if (isEquipped && playerRotation != null && spotLight != null)
        //{
        //    if (playerRotation.CameraGroup != null)
        //    {
        //        // ?? 회전(Rotation)만 맞추면 손전등 본체가 저 뒤에 남았을 때 빛의 시작점이 깨집니다.
        //        // 불빛의 위치(Position)까지 실시간으로 플레이어 눈(CameraGroup)의 좌표로 강제 워프(Snap)시킵니다.
        //        spotLight.transform.position = playerRotation.CameraGroup.transform.position;
        //        spotLight.transform.rotation = playerRotation.CameraGroup.transform.rotation;
        //    }
        //}

    }
    void LateUpdate()
    {
        if (isEquipped && netTransform != null && netTransform.enabled)
        {
            netTransform.enabled = false;
        }

        // 2. [멀미 방지] 불빛(SpotLight)만 플레이어 시야(CameraGroup)를 부드럽게 추적
        if (isEquipped && playerRotation != null && spotLight != null)
        {
            if (playerRotation.CameraGroup != null)
            {
                spotLight.transform.position = playerRotation.CameraGroup.transform.position;
                spotLight.transform.rotation = playerRotation.CameraGroup.transform.rotation;
            }
        }
    }

    void ForceTurnOff()
    {
        if (IsOwner) ForceTurnOffServerRpc();
    }

    [Rpc(SendTo.Server)]
    void ForceTurnOffServerRpc()
    {
        ForceTurnOffClientRpc();
    }

    [Rpc(SendTo.Everyone)]
    void ForceTurnOffClientRpc()
    {
        // 주인(Owner)은 위에서 먼저 껐으므로, 주인이 아닌 다른 플레이어들의 화면에서만 불을 꺼줍니다.
        if (!IsOwner && spotLight != null)
        {
            spotLight.enabled = false;
        }

        // [TODO] 배터리 방전 지지직 소리 재생 (전원 꺼짐 연출)
    }

    // 충전기 연동 대비
    public void Recharge()
    {
        if (IsOwner) currentBatteryNet.Value = maxBattery;
        Debug.Log($"<color=cyan>[Flashlight]</color> {itemData.itemName} 충전 완료!");
    }

    // ==========================================
    // 3. 세이브 & 로드 연동
    // ==========================================
    public override float[] ExtractSaveData()
    {
        // 네트워크 변수의 값을 추출해서 저장
        return new float[] { currentBatteryNet.Value };
    }

    public override void ApplySaveData(float[] savedStates)
    {
        // 서버인 경우에만 세이브 데이터를 네트워크 변수에 덮어씁니다.
        if (IsServer && savedStates != null && savedStates.Length > 0)
        {
            currentBatteryNet.Value = savedStates[0];
        }
    }
}