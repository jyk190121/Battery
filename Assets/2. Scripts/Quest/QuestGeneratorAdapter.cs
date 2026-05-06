using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class QuestGeneratorAdapter : NetworkBehaviour
{
    [Header("Teammate Code Reference")]
    [Tooltip("같은 오브젝트에 있는 팀원분의 발전기 스크립트를 드래그해 넣으세요.")]
    public GeneratorController teammateGenerator;

    [Header("Quest Settings")]
    public int floorLevel = 1;       // 이 발전기가 위치한 층 (1~2층 Easy, 3층 이상 Normal/Hard)
    public int repairPartItemID;     // 수리 부속 아이템 ID

    [System.Serializable]
    public struct RoomSpawnPoint
    {
        public DoorController door;
        public Transform spawnLocation; // 이 문이 열렸을 때 타겟 아이템이 스폰될 방 안쪽 좌표
    }
    [Header("Room Setup")]
    [Tooltip("팀원 코드가 열 수 있는 문들과, 그 방 안쪽의 스폰 좌표를 매칭해주세요.")]
    public List<RoomSpawnPoint> roomSpawnPoints;

    // 동기화 변수들
    public NetworkVariable<bool> isQuestTarget = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentParts = new NetworkVariable<int>(0);
    public NetworkVariable<int> requiredParts = new NetworkVariable<int>(99);

    private QuestDataSO currentQuestData;

    public override void OnNetworkSpawn()
    {
        // 💡 [핵심] 팀원 코드의 수리 완료 이벤트를 감시합니다.
        if (teammateGenerator != null)
        {
            teammateGenerator.isRepaired.OnValueChanged += OnTeammateGeneratorRepaired;
        }
    }

    // 아침이 되어 매니저가 이 발전기를 타겟으로 지목할 때 호출
    public void SetupQuestTarget(QuestDataSO questData)
    {
        if (!IsServer) return;
        currentQuestData = questData;
        requiredParts.Value = questData.materialCount; // 기획된 2, 3, 4개
        currentParts.Value = 0;
        isQuestTarget.Value = true;
    }

    // 플레이어가 수리 부속을 들고 상호작용(E) 할 때 호출
    public void Interact(PlayerInventory player)
    {
        if (!isQuestTarget.Value || teammateGenerator.isRepaired.Value) return;

        ItemBase held = player.HeldItem;
        if (held != null && held.itemData.itemID == repairPartItemID)
        {
            InsertPartServerRpc(player.OwnerClientId);
        }
        else
        {
            Debug.Log($"<color=orange>[Quest]</color> 수리 부속({repairPartItemID})이 필요합니다! ({currentParts.Value}/{requiredParts.Value})");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InsertPartServerRpc(ulong clientId)
    {
        if (teammateGenerator.isRepaired.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();

            // 인벤토리에서 부속품 1개 정상 차감 성공 시
            if (inv != null && inv.RemoveItemByServer(repairPartItemID))
            {
                currentParts.Value++;
                Debug.Log($"<color=cyan>[Quest]</color> 부속 장착됨! ({currentParts.Value}/{requiredParts.Value})");

                // 목표 개수 도달 시
                if (currentParts.Value >= requiredParts.Value)
                {
                    // 1. 팀원 코드 강제 작동! (레버 돌아가고 랜덤으로 문이 열림)
                    teammateGenerator.CompleteRepairServerRpc();

                    // 2. Easy 난이도면 그 즉시 퀘스트 클리어
                    if (currentQuestData != null && currentQuestData.difficulty == QuestDifficulty.Easy)
                    {
                        QuestManager.Instance.NotifyCustomQuestMet(currentQuestData.questID, clientId);
                        Debug.Log("<color=lime>[Quest]</color> 발전기 가동! (Easy) 퀘스트 즉시 완료.");
                    }
                }
            }
        }
    }

    // 팀원 코드가 문을 열었을 때 자동으로 반응하는 콜백 함수
    private void OnTeammateGeneratorRepaired(bool previousValue, bool newValue)
    {
        if (!IsServer || !newValue || !isQuestTarget.Value) return;

        // Normal / Hard 난이도일 경우, 새로 열린 방 안에 타겟 수집 아이템 소환
        if (currentQuestData != null && currentQuestData.difficulty != QuestDifficulty.Easy)
        {
            StartCoroutine(SpawnItemBehindUnlockedDoorRoutine());
        }
    }

    private IEnumerator SpawnItemBehindUnlockedDoorRoutine()
    {
        // 팀원 코드가 랜덤하게 문을 열어주는 시간을 살짝 대기
        yield return new WaitForSeconds(0.5f);

        // 방금 열린 문을 추적
        foreach (var room in roomSpawnPoints)
        {
            if (room.door != null && room.door.isOpen.Value)
            {
                // 해당 문 방 안쪽에 퀘스트 아이템(1손 or 양손 수집품) 소환
                ItemBase prefab = GameSessionManager.Instance.GetPrefab(currentQuestData.targetItemID);
                if (prefab != null && room.spawnLocation != null)
                {
                    ItemBase spawned = Instantiate(prefab, room.spawnLocation.position, room.spawnLocation.rotation);
                    spawned.GetComponent<NetworkObject>().Spawn();
                    Debug.Log($"<color=lime>[Quest]</color> {room.door.name} 내부에 타겟 아이템({prefab.itemData.itemName}) 스폰 완료. 가져오십시오!");
                }
                break;
            }
        }
    }
}