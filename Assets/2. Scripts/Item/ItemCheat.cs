using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// 아이템 소환 및 경제 시스템 치트를 담당하는 스크립트입니다.
/// </summary>
public class ItemCheat : NetworkBehaviour
{
    [Header("Item Spawn Settings")]
    public int targetItemID = 501; // 소환할 아이템 ID

    [Header("Debug Keys")]
    public string helpMessage = "F2: 아이템 소환 | F5: 신용한도 추가";

    void Update()
    {
        // 서버(Host) 권한이 있는 경우에만 치트키 작동
        if (!IsServer || Keyboard.current == null) return;

        // F2: 목표 아이템 내 눈앞에 소환
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            SpawnItemAtCamera(targetItemID);
        }

        // F5: 디버그용 자금(신용 한도) 추가
        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            AddDebugMoney(1000);
        }
    }

    private void SpawnItemAtCamera(int itemID)
    {
        if (GameSessionManager.Instance == null) return;

        ItemBase prefab = GameSessionManager.Instance.GetPrefab(itemID);
        if (prefab != null && Camera.main != null)
        {
            Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 2f;
            ItemBase spawned = Instantiate(prefab, spawnPos, Quaternion.identity);

            // 네트워크 스폰
            spawned.GetComponent<NetworkObject>().Spawn();

            Debug.Log($"<color=orange>[CHEAT]</color> 아이템 {itemID}번({prefab.itemData.itemName}) 소환 완료.");
        }
        else
        {
            Debug.LogError($"<color=red>[CHEAT 실패]</color> {itemID}번 프리팹을 찾을 수 없거나 카메라가 없습니다.");
        }
    }

    private void AddDebugMoney(int amount)
    {
        if (GameMaster.Instance != null && GameMaster.Instance.economyManager != null)
        {
            GameMaster.Instance.economyManager.availableLoanLimit.Value += amount;
            Debug.Log($"<color=green>[CHEAT]</color> 자금 {amount} 추가! 현재 한도: {GameMaster.Instance.economyManager.availableLoanLimit.Value}");
        }
    }
}