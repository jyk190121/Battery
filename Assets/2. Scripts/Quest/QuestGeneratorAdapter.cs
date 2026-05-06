using UnityEngine;
using Unity.Netcode;

public class QuestGeneratorAdapter : NetworkBehaviour
{
    [Header("Core Reference")]
    public GeneratorController baseGenerator;

    [Header("Quest Settings")]
    public int floorLevel = 1;
    public int repairPartItemID;

    public NetworkVariable<bool> isQuestTarget = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentParts = new NetworkVariable<int>(0);
    public NetworkVariable<int> requiredParts = new NetworkVariable<int>(99);

    private QuestDataSO currentQuestData;

    public override void OnNetworkSpawn()
    {
        isQuestTarget.OnValueChanged += OnQuestTargetStatusChanged;
        if (baseGenerator != null)
        {
            baseGenerator.isRepaired.OnValueChanged += OnBaseGeneratorRepaired;
        }

        RefreshInteractionState(isQuestTarget.Value);
    }

    public override void OnNetworkDespawn()
    {
        isQuestTarget.OnValueChanged -= OnQuestTargetStatusChanged;
        if (baseGenerator != null)
        {
            baseGenerator.isRepaired.OnValueChanged -= OnBaseGeneratorRepaired;
        }
    }

    private void OnQuestTargetStatusChanged(bool previousValue, bool newValue)
    {
        RefreshInteractionState(newValue);
    }

    private void RefreshInteractionState(bool isTarget)
    {
        if (baseGenerator == null) return;

        // 타겟이면서 수리되지 않은 상태면 팀원 발전기(Hold E) 비활성화
        baseGenerator.enabled = !(isTarget && !baseGenerator.isRepaired.Value);
    }

    // 퀘스트 지정 및 조기 소환 처리
    public void SetupQuestTarget(QuestDataSO questData)
    {
        if (!IsServer) return;
        currentQuestData = questData;
        requiredParts.Value = questData.materialCount;
        currentParts.Value = 0;
        isQuestTarget.Value = true;

        if (questData.difficulty != QuestDifficulty.Easy)
        {
            SpawnItemInLinkedRoom();
        }
    }

    // 발전기와 연결된 문 스크립트를 확인하고 특수 룸에 수집 아이템 소환
    private void SpawnItemInLinkedRoom()
    {
        if (baseGenerator == null || baseGenerator.linkableDoors == null || baseGenerator.linkableDoors.Count == 0) return;

        int randomIndex = Random.Range(0, baseGenerator.linkableDoors.Count);
        DoorController targetDoor = baseGenerator.linkableDoors[randomIndex];

        ItemBase prefab = GameSessionManager.Instance.GetPrefab(currentQuestData.targetItemID);
        if (prefab != null && targetDoor.questItemSpawnPoint != null)
        {
            ItemBase spawned = Instantiate(prefab, targetDoor.questItemSpawnPoint.position, targetDoor.questItemSpawnPoint.rotation);
            spawned.GetComponent<NetworkObject>().Spawn();

            Debug.Log($"[Quest] {targetDoor.roomLocation} 내부에 수집 퀘스트 아이템 소환 완료.");
        }
    }

    public void Interact(PlayerInventory player)
    {
        if (!isQuestTarget.Value || baseGenerator.isRepaired.Value) return;

        ItemBase held = player.HeldItem;
        if (held != null && held.itemData.itemID == repairPartItemID)
        {
            InsertPartServerRpc(player.OwnerClientId);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InsertPartServerRpc(ulong clientId)
    {
        if (baseGenerator.isRepaired.Value || currentParts.Value >= requiredParts.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();

            if (inv != null && inv.RemoveItemByServer(repairPartItemID))
            {
                currentParts.Value++;
                Debug.Log($"[Quest] 부속 장착 완료 ({currentParts.Value}/{requiredParts.Value})");

                if (currentParts.Value >= requiredParts.Value)
                {
                    // 기믹 해결 후 팀원 발전기 코드를 활성화하여 문을 열 수 있도록 함
                    baseGenerator.enabled = true;
                    Debug.Log("[Quest] 발전기 수동 가동 대기 중.");
                }
            }
        }
    }

    // 팀원 발전기 코드 동작 후 클리어 처리
    private void OnBaseGeneratorRepaired(bool previousValue, bool newValue)
    {
        if (!IsServer || !newValue || !isQuestTarget.Value) return;

        if (currentQuestData != null && currentQuestData.difficulty == QuestDifficulty.Easy)
        {
            QuestManager.Instance.NotifyCustomQuestMet(currentQuestData.questID, NetworkManager.ServerClientId);
            Debug.Log("[Quest] 발전기 가동(Easy) 클리어 통보.");
        }
    }
}