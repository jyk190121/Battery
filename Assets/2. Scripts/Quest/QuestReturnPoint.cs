using Unity.Netcode;
using UnityEngine;
using System.Collections;

/// <summary>
/// 특정 아이템을 반납하고 영혼 세계(Spiritual World)로 텔레포트하는 환원 퀘스트 상호작용 포인트를 관리.
/// </summary>
public class QuestReturnPoint : NetworkBehaviour
{
    public static event System.Action OnSpiritualWorldEntered;

    [Header("Teleport Settings")]
    public float returnDelay = 60f;
    public Vector3 spiritWorldPos = new Vector3(1100f, 1f, 135f);

    [Header("Quest Settings")]
    public int[] targetQuestIDs = { 1040, 2040, 3040 };
    public int requiredItemID;

    [Header("Visual Components")]
    public GameObject ghostModel;
    public GameObject realModel;
    public Outline outline;

    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> hasItem = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isActivatedByManager = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        if (QuestManager.Instance != null)
        {
            foreach (int questId in targetQuestIDs)
            {
                QuestManager.Instance.RegisterReturnPoint(questId, this);
            }
        }

        RefreshState(hasItem.Value, isCompleted.Value);

        hasItem.OnValueChanged += (prev, next) => RefreshState(next, isCompleted.Value);
        isCompleted.OnValueChanged += (prev, next) => RefreshState(hasItem.Value, next);
        isActivatedByManager.OnValueChanged += (prev, next) => RefreshState(hasItem.Value, isCompleted.Value);
    }

    public void SetPointActivation(bool isActive)
    {
        if (IsServer)
        {
            isActivatedByManager.Value = isActive;
        }
    }

    public bool IsInteractable()
    {
        return isActivatedByManager.Value && !isCompleted.Value;
    }

    public void Interact(PlayerInventory playerInventory)
    {
        if (!IsInteractable()) { return; }

        if (!hasItem.Value)
        {
            ItemBase heldItem = playerInventory.HeldItem;
            if (heldItem == null || heldItem.itemData.itemID != requiredItemID)
            {
                Debug.Log($"<color=orange>[Quest] {requiredItemID}번 아이템을 손에 들어야 작동합니다!</color>");
                return;
            }

            TryReturnItemServerRpc(playerInventory.OwnerClientId);
        }
        else
        {
            TryFinalClearServerRpc(playerInventory.OwnerClientId);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryReturnItemServerRpc(ulong clientId)
    {
        if (hasItem.Value) { return; }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inventory != null && inventory.RemoveItemByServer(requiredItemID))
            {
                hasItem.Value = true;
                Debug.Log($"<color=cyan>[Quest]</color> Client {clientId} 아이템 반납 완료. 다음 클릭 시 영혼 세계 이동.");
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryFinalClearServerRpc(ulong clientId)
    {
        if (isCompleted.Value || !hasItem.Value) { return; }

        isCompleted.Value = true;
        OnSpiritualWorldEntered?.Invoke();

        int activeQuestId = 0;
        foreach (int questId in targetQuestIDs)
        {
            if (QuestManager.Instance.activeQuests.Contains(questId))
            {
                activeQuestId = questId;
                break;
            }
        }

        float dynamicDelay = returnDelay;
        if (activeQuestId == 2040)
        {
            dynamicDelay = 90f;
        }
        else if (activeQuestId == 3040)
        {
            dynamicDelay = 120f;
        }

        if (activeQuestId != 0)
        {
            QuestManager.Instance.NotifyCustomQuestMet(activeQuestId, clientId);
        }

        NotifyTeleportLogClientRpc(clientId, dynamicDelay);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyTeleportLogClientRpc(ulong clientId, float delayTime)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient))
        {
            NetworkObject playerObj = networkClient.PlayerObject;

            if (playerObj.IsOwner)
            {
                if (playerObj.TryGetComponent(out Unity.Netcode.Components.NetworkTransform networkTransform))
                {
                    StartCoroutine(TeleportAndReturnRoutine(playerObj, delayTime));
                }
                else
                {
                    playerObj.transform.position = spiritWorldPos;
                }
            }
        }
    }

    private IEnumerator TeleportAndReturnRoutine(NetworkObject playerObj, float delayTime)
    {
        if (playerObj.TryGetComponent(out Unity.Netcode.Components.NetworkTransform networkTransform))
        {
            Vector3 originalPos = playerObj.transform.position;
            Quaternion originalRot = playerObj.transform.rotation;

            networkTransform.Teleport(spiritWorldPos, Quaternion.identity, playerObj.transform.localScale);
            Debug.Log($"<color=purple>[Gimmick]</color> 영혼 세계 진입. {delayTime}초 후 복귀합니다.");

            yield return new WaitForSeconds(delayTime);

            networkTransform.Teleport(originalPos, originalRot, playerObj.transform.localScale);
            Debug.Log("<color=purple>[Gimmick]</color> 시간이 다 되어 원래 세계로 돌아왔습니다.");
        }
    }

    private void RefreshState(bool itemReturned, bool isPointCompleted)
    {
        if (realModel != null)
        {
            realModel.SetActive(itemReturned);
        }

        if (ghostModel != null)
        {
            ghostModel.SetActive(isActivatedByManager.Value && !itemReturned);
        }

        if (PlayerInventory.LocalInstance != null)
        {
            PlayerInventory.LocalInstance.ClearHighlight();
        }

        if (isPointCompleted)
        {
            if (TryGetComponent(out Collider pointCollider))
            {
                pointCollider.enabled = false;
            }
            if (outline != null)
            {
                outline.enabled = false;
            }
        }
    }
}