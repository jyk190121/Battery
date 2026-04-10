using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Text;

public class QuestDebugManager : NetworkBehaviour
{
    [Header("Test Target Settings")]
    public int targetQuestID = 101;     // 테스트할 퀘스트 번호
    public int targetItemID = 501;      // 테스트할 아이템 번호 (환원/수집용)

    [Header("Debug Keys")]
    public string helpMessage = "F1:수락, F2:템소환, F3:강제완료, F4:상태출력, F5:돈추가";
    void Update()
    {
        // 서버(Host) 권한이 있는 경우에만 치트키 작동
        if (!IsServer || Keyboard.current == null) return;

        // F1: 퀘스트 강제 수락 (태블릿 UI 대용)
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            QuestManager.Instance.AcceptQuestFromTablet(targetQuestID);
            Debug.Log($"<color=yellow>[DEBUG]</color> 퀘스트 {targetQuestID} 수락됨.");
        }

        // F2: 목표 아이템 내 눈앞에 소환 (상점 구매 대용)
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            SpawnItemAtCamera(targetItemID);
        }

        // F3: 현재 퀘스트 강제 완료 처리 (기믹 통과 대용)
        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            QuestManager.Instance.NotifyCustomQuestMet(targetQuestID, NetworkManager.ServerClientId);
            Debug.Log($"<color=cyan>[DEBUG]</color> 퀘스트 {targetQuestID} 강제 클리어 보고 완료.");
        }

        // F4: 현재 모든 퀘스트 상태 브리핑 (콘솔 출력)
        if (Keyboard.current.f4Key.wasPressedThisFrame)
        {
            PrintQuestStatus();
        }

        // F5: 디버그용 자금 추가
        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            GameSessionManager.Instance.AddMoney(1000);
            Debug.Log($"<color=green>[DEBUG]</color> 자산 1000원 추가. 현재 잔액: {GameSessionManager.Instance.currentMoney}");
        }
    }

    private void SpawnItemAtCamera(int itemID)
    {
        ItemBase prefab = GameSessionManager.Instance.GetPrefab(itemID);
        if (prefab != null && Camera.main != null)
        {
            Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 2f;
            ItemBase spawned = Instantiate(prefab, spawnPos, Quaternion.identity);
            spawned.GetComponent<NetworkObject>().Spawn();
            Debug.Log($"<color=orange>[DEBUG]</color> 아이템 {itemID}번 소환 완료.");
        }
        else
        {
            Debug.LogError($"<color=red>[DEBUG 실패]</color> {itemID}번 프리팹을 찾을 수 없거나 카메라가 없습니다. GameSessionManager의 DB를 확인하세요.");
        }
    }

    private void PrintQuestStatus()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b><color=white>=== 현재 퀘스트 상태 브리핑 ===</color></b>");

        sb.Append("수락된 퀘스트: ");
        foreach (var id in QuestManager.Instance.activeQuests) sb.Append($"[{id}] ");
        sb.AppendLine();

        sb.Append("완료된 퀘스트: ");
        foreach (var id in QuestManager.Instance.serverCompletedQuests) sb.Append($"<color=green>[{id}]</color> ");
        sb.AppendLine();

        int reward = QuestManager.Instance.GetCalculatedQuestReward();
        sb.AppendLine($"예상 정산 보너스: <color=yellow>{reward}원</color>");

        Debug.Log(sb.ToString());
    }
}