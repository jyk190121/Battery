using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class QuestGeneratorAdapter : NetworkBehaviour
{
    [Header("Core Reference")]
    public GeneratorController baseGenerator;

    [Header("Quest Settings")]
    public int repairPartItemID = 401; // 수리 부속 아이템 ID

    public NetworkVariable<bool> isQuestTarget = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentParts = new NetworkVariable<int>(0);
    public NetworkVariable<int> requiredParts = new NetworkVariable<int>(0);

    private QuestDataSO currentQuestData; 

    public override void OnNetworkSpawn()
    {
        if (baseGenerator != null)
            baseGenerator.isRepaired.OnValueChanged += OnBaseGeneratorRepaired;

        // 발전기 컴포넌트는 항상 켜둔다 (상호작용 감지용)
        if (baseGenerator != null) baseGenerator.enabled = true;
    }

    // 퀘스트용 발전기 셋업 (assignedDoor에 기본값 null 추가)
    // 매개변수 2개를 명시적으로 선언하고 assignedDoor에 null 허용
    public void SetupQuestTarget(QuestDataSO questData, DoorController assignedDoor = null)
    {
        if (!IsServer) return;

        currentQuestData = questData;
        requiredParts.Value = questData.materialCount;
        currentParts.Value = 0;
        isQuestTarget.Value = true;

        // 중앙 매칭에서 문을 정해준 경우 리스트 고정
        if (assignedDoor != null)
        {
            baseGenerator.linkableDoors.Clear();
            baseGenerator.linkableDoors.Add(assignedDoor);
        }

        // 아이템 소환 대상 결정
        DoorController targetSpawnDoor = assignedDoor;
        if (targetSpawnDoor == null && baseGenerator.linkableDoors.Count > 0)
        {
            targetSpawnDoor = baseGenerator.linkableDoors.Find(d => d.questItemSpawnPoint != null);
        }

        // 아이템 소환 (Easy 제외 및 유효한 문 확인)
        if (questData.difficulty != QuestDifficulty.Easy && targetSpawnDoor != null && targetSpawnDoor.questItemSpawnPoint != null)
        {
            ItemBase prefab = GameSessionManager.Instance.GetPrefab(questData.targetItemID);
            if (prefab != null)
            {
                ItemBase spawned = Instantiate(prefab, targetSpawnDoor.questItemSpawnPoint.position, targetSpawnDoor.questItemSpawnPoint.rotation);
                spawned.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
    // 일반 발전기용 셋업 (추가)
    public void SetupNormalGenerator(DoorController assignedDoor)
    {
        if (!IsServer) return;

        isQuestTarget.Value = false; // 일반 발전기임 명시

        // 리스트를 비우고 전달받은 문 하나만 삽입
        baseGenerator.linkableDoors.Clear();
        baseGenerator.linkableDoors.Add(assignedDoor);

        // 일반 발전기는 부속 필요 없으므로 즉시 가동 가능하게 설정
        baseGenerator.enabled = true;
    }

    // 누락되었던 핵심 소환 로직 복구
    private void SpawnItemInLinkedRoom()
    {
        if (baseGenerator == null || baseGenerator.linkableDoors == null) return;

        List<DoorController> validQuestDoors = new List<DoorController>();
        foreach (var door in baseGenerator.linkableDoors)
        {
            if (door != null && door.questItemSpawnPoint != null)
                validQuestDoors.Add(door);
        }

        if (validQuestDoors.Count == 0)
        {
            Debug.LogError("[Generator Debug] 스폰 포인트가 할당된 문이 없어 소환 불가.");
            return;
        }

        DoorController targetDoor = validQuestDoors[Random.Range(0, validQuestDoors.Count)];

        ItemBase prefab = GameSessionManager.Instance.GetPrefab(currentQuestData.targetItemID);
        if (prefab != null)
        {
            ItemBase spawned = Instantiate(prefab, targetDoor.questItemSpawnPoint.position, targetDoor.questItemSpawnPoint.rotation);
            spawned.GetComponent<NetworkObject>().Spawn();
            Debug.Log($"<color=lime>[Quest]</color> {targetDoor.roomLocation} 내부에 타겟 아이템 소환 완료.");
        }

        // 문 개방 대상을 해당 방으로 고정
        baseGenerator.linkableDoors.Clear();
        baseGenerator.linkableDoors.Add(targetDoor);
    }

    // 플레이어가 바라볼 때 UI에 띄울 텍스트
    public string GetInteractText()
    {
        if (!isQuestTarget.Value) return null; // 퀘스트 대상 아니면 기본 텍스트 출력됨

        if (baseGenerator.isRepaired.Value) return "발전기 수리 완료";

        if (currentParts.Value < requiredParts.Value)
        {
            return $"수리 부속이 필요합니다 ({currentParts.Value}/{requiredParts.Value})";
        }

        return "발전기 가동 준비 완료 (Hold E)";
    }

    public void Interact(PlayerInventory player)
    {
        if (!isQuestTarget.Value || baseGenerator.isRepaired.Value) return;

        ItemBase held = player.HeldItem;

        // 올바른 아이템을 들고 있는 경우
        if (held != null && held.itemData.itemID == repairPartItemID)
        {
            InsertPartServerRpc(player.OwnerClientId);
        }
        // 아이템이 없거나 잘못된 아이템인 경우 피드백
        else
        {
            Debug.Log($"<color=orange>[Quest]</color> 수리 부속({repairPartItemID})이 필요합니다. 현재 진행도: {currentParts.Value}/{requiredParts.Value}");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InsertPartServerRpc(ulong clientId)
    {
        if (currentParts.Value >= requiredParts.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv != null && inv.RemoveItemByServer(repairPartItemID))
            {
                currentParts.Value++;
            }
        }
    }

    private void OnBaseGeneratorRepaired(bool previous, bool current)
    {
        if (!IsServer || !current || !isQuestTarget.Value) return;

        if (currentParts.Value < requiredParts.Value)
        {
            // 부품이 부족한데 수리 완료가 되면 강제로 되돌림 (핵 방지 및 로직 보호)
            baseGenerator.isRepaired.Value = false;
            return;
        }

        if (currentQuestData != null && currentQuestData.difficulty == QuestDifficulty.Easy)
        {
            QuestManager.Instance.NotifyCustomQuestMet(currentQuestData.questID, NetworkManager.ServerClientId);
        }
    }
}