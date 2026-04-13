using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class QuestExploreZone : NetworkBehaviour
{
    public int targetQuestID;
    public float requiredStayTime = 10f;

    [Header("Layer Settings")]
    [Tooltip("플레이어로 인식할 레이어를 선택하세요")]
    public LayerMask playerLayer; // 💡 태그 대신 레이어 마스크 사용

    private HashSet<ulong> playersInZone = new HashSet<ulong>();
    private Coroutine exploreCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // 💡 1. 들어온 객체의 레이어가 playerLayer에 포함되어 있는지 비트 연산으로 확인
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            // 💡 2. 자식 콜라이더가 닿았을 수도 있으므로 InParent로 최상위 NetworkObject 탐색
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null)
            {
                playersInZone.Add(netObj.OwnerClientId);

                if (playersInZone.Count == 1) exploreCoroutine = StartCoroutine(ExploreTimerRoutine());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        // 💡 레이어 확인
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null)
            {
                playersInZone.Remove(netObj.OwnerClientId);

                if (playersInZone.Count == 0 && exploreCoroutine != null)
                {
                    StopCoroutine(exploreCoroutine);
                    exploreCoroutine = null;
                    NotifyTimerStateClientRpc(false);
                }
            }
        }
    }

    private IEnumerator ExploreTimerRoutine()
    {
        NotifyTimerStateClientRpc(true);

        float timer = 0f;
        while (timer < requiredStayTime)
        {
            yield return new WaitForSeconds(1f);
            timer += 1f;

            // 연결이 끊긴 유저 정리
            playersInZone.RemoveWhere(id => !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

            if (playersInZone.Count == 0)
            {
                exploreCoroutine = null;
                NotifyTimerStateClientRpc(false);
                yield break;
            }
        }

        ulong solverId = NetworkManager.ServerClientId;
        foreach (var id in playersInZone) { solverId = id; break; }

        QuestManager.Instance.NotifyCustomQuestMet(targetQuestID, solverId);
        exploreCoroutine = null;
        NotifyTimerStateClientRpc(false);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyTimerStateClientRpc(bool isStarting) { }

    private void OnDrawGizmos()
    {
        if (TryGetComponent(out BoxCollider col))
        {
            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
        }
    }
}