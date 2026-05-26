using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class QuestReturnPoint : NetworkBehaviour
{
    public static event System.Action OnSpiritualWorldEntered;

    [Header("Teleport Settings")]
    public float returnDelay = 60f; // 1분 뒤 복귀
    public Vector3 spiritWorldPos = new Vector3(1100f, 1f, 135f);

    [Header("Quest Settings")]
    public int[] targetQuestIDs = { 1040, 2040, 3040 }; // 이지, 노말, 하드 ID 배열 처리
    public int requiredItemID;

    [Header("Visual Components")]
    public GameObject ghostModel;
    public GameObject realModel;
    public Outline outline;

    // [동기화 데이터] 
    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);       // 최종 클리어 (2단계 완료)
    private NetworkVariable<bool> hasItem = new NetworkVariable<bool>(false);            // 아이템 반납됨 (1단계 완료)
    private NetworkVariable<bool> isActivatedByManager = new NetworkVariable<bool>(false);
   
    private bool hasResetOutlineForRealModel = false;
    public override void OnNetworkSpawn()
    {
        if (QuestManager.Instance != null)
        {
            // 배열에 있는 모든 퀘스트 ID 등록
            foreach (int id in targetQuestIDs)
            {
                QuestManager.Instance.RegisterReturnPoint(id, this);
            }
        }

        // 데이터 변경 시 화면 갱신 연결
        RefreshState(hasItem.Value, isCompleted.Value);

        hasItem.OnValueChanged += (prev, next) => RefreshState(next, isCompleted.Value);
        isCompleted.OnValueChanged += (prev, next) => RefreshState(hasItem.Value, next);
        isActivatedByManager.OnValueChanged += (prev, next) => RefreshState(hasItem.Value, isCompleted.Value);
    }

    public void SetPointActivation(bool isActive)
    {
        if (IsServer) isActivatedByManager.Value = isActive;
    }

    public bool IsInteractable()
    {
        return isActivatedByManager.Value && !isCompleted.Value;
    }

    public void Interact(PlayerInventory player)
    {
        if (!IsInteractable()) return;

        // 1단계: 아이템 반납이 아직 안 된 경우
        if (!hasItem.Value)
        {
            ItemBase held = player.HeldItem;
            if (held == null || held.itemData.itemID != requiredItemID)
            {
                Debug.Log($"<color=orange>[Quest] {requiredItemID}번 아이템을 손에 들어야 작동합니다!</color>");
                return;
            }
            // 아이템 반납 RPC 호출
            TryReturnItemServerRpc(player.OwnerClientId);
        }
        // 2단계: 아이템은 이미 있고, 다시 한번 눌러서 영혼 세계 이동 및 클리어
        else
        {
            TryFinalClearServerRpc(player.OwnerClientId);
        }
    }

    // [1단계] 아이템 반납 처리 RPC
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryReturnItemServerRpc(ulong clientId)
    {
        if (hasItem.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv != null && inv.RemoveItemByServer(requiredItemID))
            {
                hasItem.Value = true; // 아이템 반납 상태로 변경 (모델 교체됨)
                Debug.Log($"<color=cyan>[Quest]</color> Client {clientId} 아이템 반납 완료. 다음 클릭 시 영혼 세계 이동.");
            }
        }
    }

    // [2단계] 최종 상호작용 (이동 및 퀘스트 클리어) RPC
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryFinalClearServerRpc(ulong clientId)
    {
        if (isCompleted.Value || !hasItem.Value) return;

        isCompleted.Value = true; // 최종 완료

        OnSpiritualWorldEntered?.Invoke();

        // 퀘스트 매니저 클리어 보고 (현재 활성화된 난이도 퀘스트 찾기)
        int activeId = 0;
        foreach (int id in targetQuestIDs)
        {
            if (QuestManager.Instance.activeQuests.Contains(id))
            {
                activeId = id;
                break;
            }
        }

        //난이도 밸런싱
        float dynamicDelay = returnDelay; // 기본 60초 (1040 이지)
        if (activeId == 2040) dynamicDelay = 90f;      // 노말
        else if (activeId == 3040) dynamicDelay = 120f; // 하드

        if (activeId != 0)
        {
            QuestManager.Instance.NotifyCustomQuestMet(activeId, clientId);
        }

        // 이동 로그 및 연출 알림
        NotifyTeleportLogClientRpc(clientId, dynamicDelay);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyTeleportLogClientRpc(ulong clientId, float delayTime)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient))
        {
            //이동시킬 플레이어 찾기
            var playerObj = networkClient.PlayerObject;

            // 내 로컬 화면에서 내가 이동해야 하는 경우인지 확인 (Owner 권한 기반 이동)
            if (playerObj.IsOwner)
            {
                // 플레이어 오브젝트에 붙은 NetworkTransform을 가져옵니다.
                if (playerObj.TryGetComponent(out Unity.Netcode.Components.NetworkTransform nt))
                {
                    StartCoroutine(TeleportAndReturnRoutine(playerObj, delayTime));
                }
                else
                {
                    // NT가 없을 경우 수동 이동 (권한 체크 필수)
                    playerObj.transform.position = spiritWorldPos;
                }
            }
        }
    }

    IEnumerator TeleportAndReturnRoutine(NetworkObject playerObj, float delayTime)
    {
        if (playerObj.TryGetComponent(out Unity.Netcode.Components.NetworkTransform nt))
        {
            Vector3 originalPos = playerObj.transform.position;
            Quaternion originalRot = playerObj.transform.rotation;

            nt.Teleport(spiritWorldPos, Quaternion.identity, playerObj.transform.localScale);
            Debug.Log($"<color=purple>[Gimmick]</color> 영혼 세계 진입. {delayTime}초 후 복귀합니다.");

            // 동적 할당된 난이도별 시간 적용
            yield return new WaitForSeconds(delayTime);

            nt.Teleport(originalPos, originalRot, playerObj.transform.localScale);
            Debug.Log("<color=purple>[Gimmick]</color> 시간이 다 되어 원래 세계로 돌아왔습니다.");
        }
    }

    private void RefreshState(bool itemReturned, bool completed)
    {
        if (realModel != null) realModel.SetActive(itemReturned);
        if (ghostModel != null) ghostModel.SetActive(isActivatedByManager.Value && !itemReturned);

        // [최초 1회 실행] 1단계 완료(itemReturned) 시점에 딱 한 번만 아웃라인 재시동
        if (itemReturned && !completed && !hasResetOutlineForRealModel)
        {
            hasResetOutlineForRealModel = true; // 자물쇠 잠금 (이후 두 번 다시 실행 안 됨)

            // 플레이어가 이 순간 쳐다보고 있어서 이미 켜져있다면 메쉬 재수집을 위해 껐다 켬
            if (outline != null && outline.enabled)
            {
                outline.enabled = false;
                outline.enabled = true;
            }
        }

        if (completed)
        {
            if (TryGetComponent(out Collider col)) col.enabled = false;
            if (outline != null) outline.enabled = false;
        }
    }
}