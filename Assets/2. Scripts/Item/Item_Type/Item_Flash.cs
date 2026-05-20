using UnityEngine;
using Unity.Netcode;

public class Item_Flash : ItemBase
{
    [Header("Flashlight Settings")]
    [Tooltip("손전등 불빛을 쏠 Light 컴포넌트")]
    public Light spotLight;

    [Tooltip("최대 배터리량 (내구도)")]
    public float maxBattery = 100f;

    [Tooltip("초당 배터리 소모량")]
    public float batteryDrainRate = 2f;

    // 현재 배터리
    private float _currentBattery;

    protected override void Awake()
    {
        base.Awake();
        _currentBattery = maxBattery;

        if (spotLight != null) spotLight.enabled = false;
    }

    // ==========================================
    // 1. 아이템 사용
    // ==========================================
    public override void ExecuteUseItem(Vector3 direction)
    {
        base.ExecuteUseItem(direction);

        Debug.Log($"<color=yellow>[Flashlight]</color> 좌클릭 감지됨! 현재 배터리: {_currentBattery}");

        if (_currentBattery <= 0f)
        {
            Debug.Log("<color=red>[Flashlight]</color> 배터리가 없어서 켜지지 않습니다!");
            return;
        }

        if (spotLight != null)
        {
            spotLight.enabled = !spotLight.enabled;
            Debug.Log($"<color=green>[Flashlight]</color> 불빛 상태 변경됨: {spotLight.enabled}");

            // [TODO] 딸깍! 하는 스위치 사운드 재생
        }
        else
        {
            Debug.Log("<color=red>[Flashlight]</color> 에러: SpotLight가 인스펙터에 안 꽂혀 있습니다!!");
        }
    }

    // ==========================================
    // 2. 배터리 소모 (내구도)
    // ==========================================
    private void Update()
    {
        if (spotLight == null || !spotLight.enabled) return;

        // 배터리 계산은 아이템의 주인(Owner)만 계산해서 관리
        if (IsOwner && isEquipped)
        {
            _currentBattery -= batteryDrainRate * Time.deltaTime;

            // [TODO] UI에 현재 배터리량(_currentBattery / maxBattery) 업데이트

            // 배터리가 다 닳았다면 강제 종료
            if (_currentBattery <= 0f)
            {
                _currentBattery = 0f;
                ForceTurnOff();
            }
        }
    }

    // 배터리가 다 닳았을 때 서버에 요청해서 모두의 화면에서 불을 끄는 함수
    private void ForceTurnOff()
    {
        if (IsOwner) ForceTurnOffServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void ForceTurnOffServerRpc()
    {
        ForceTurnOffClientRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ForceTurnOffClientRpc()
    {
        if (spotLight != null) spotLight.enabled = false;
        // [TODO] 배터리 방전 지지직 소리 재생
    }

    // ==========================================
    // 3. 세이브 & 로드 연동 
    // ==========================================
    // 게임을 저장하거나 트럭에 보관할 때 배터리 잔량을 저장
    public override float[] ExtractSaveData()
    {
        return new float[] { _currentBattery };
    }

    public override void ApplySaveData(float[] savedStates)
    {
        if (savedStates != null && savedStates.Length > 0)
        {
            _currentBattery = savedStates[0];
        }
    }
}