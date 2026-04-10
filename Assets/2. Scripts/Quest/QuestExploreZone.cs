using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class QuestExploreZone : NetworkBehaviour
{
    public int targetQuestID;
    public float requiredStayTime = 10f; // 버텨야 하는 시간 (초)

    private HashSet<ulong> playersInZone = new HashSet<ulong>();
    private Coroutine exploreCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // 타이머 계산은 서버만 담당
        if (other.CompareTag("Player"))
        {
            playersInZone.Add(other.GetComponent<NetworkObject>().OwnerClientId);

            // 구역에 아무도 없다가 첫 번째 사람이 들어왔을 때 타이머 시작
            if (playersInZone.Count == 1) exploreCoroutine = StartCoroutine(ExploreTimerRoutine());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (other.CompareTag("Player"))
        {
            playersInZone.Remove(other.GetComponent<NetworkObject>().OwnerClientId);

            // 구역에 있던 마지막 사람마저 나갔다면 타이머 리셋
            if (playersInZone.Count == 0 && exploreCoroutine != null)
            {
                StopCoroutine(exploreCoroutine);
                exploreCoroutine = null;
                NotifyTimerStateClientRpc(false);
            }
        }
    }

    private IEnumerator ExploreTimerRoutine()
    {
        NotifyTimerStateClientRpc(true); // UI 게이지 시작 알림

        yield return new WaitForSeconds(requiredStayTime);

        // 무사히 시간을 버텼다면 구역 내 인원 중 한 명을 대표로 클리어 보고
        ulong solverId = NetworkManager.ServerClientId;
        foreach (var id in playersInZone) { solverId = id; break; }

        QuestManager.Instance.NotifyCustomQuestMet(targetQuestID, solverId);
        exploreCoroutine = null;
        NotifyTimerStateClientRpc(false); // UI 게이지 종료 알림
    }

    // UI 담당자가 게이지 바를 연동할 때 사용할 수 있는 빈 함수
    [Rpc(SendTo.Everyone)]
    private void NotifyTimerStateClientRpc(bool isStarting) { }

    // 에디터에서 구역을 시각적으로 확인하기 위한 기즈모
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