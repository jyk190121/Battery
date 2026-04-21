using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Text;

public class QuestDebugManager : NetworkBehaviour
{
    [Header("Test Target Settings")]
    public int targetQuestID = 101;     // F3 전용 (특정 퀘스트 강제 완료용)
    public int targetItemID = 501;      // F2 전용 (아이템 소환용)

    [Header("Debug Keys")]
    [TextArea]
    public string helpMessage = "F1:Easy수락, F6:Normal수락, F7:Hard수락\nF2:템소환, F3:ID강제완료, F4:상태출력\nF5:돈추가, F8:일일퀘스트 새로고침(다음날)";

    void Update()
    {
        // 서버(Host) 권한이 있고 키보드가 연결된 경우에만 작동
        if (!IsServer || Keyboard.current == null) return;

        // --- [난이도별 계약 테스트] ---

        // F1: EASY 난이도 계약 (3+1) 수락 테스트
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            QuestManager.Instance.AcceptDifficultyContractServerRpc(QuestDifficulty.Easy);
            Debug.Log("<color=yellow>[DEBUG]</color> EASY 난이도 계약 수락 시뮬레이션.");
        }

        // F6: NORMAL 난이도 계약 수락 테스트
        if (Keyboard.current.f6Key.wasPressedThisFrame)
        {
            QuestManager.Instance.AcceptDifficultyContractServerRpc(QuestDifficulty.Normal);
            Debug.Log("<color=yellow>[DEBUG]</color> NORMAL 난이도 계약 수락 시뮬레이션.");
        }

        // F7: HARD 난이도 계약 수락 테스트
        if (Keyboard.current.f7Key.wasPressedThisFrame)
        {
            QuestManager.Instance.AcceptDifficultyContractServerRpc(QuestDifficulty.Hard);
            Debug.Log("<color=yellow>[DEBUG]</color> HARD 난이도 계약 수락 시뮬레이션.");
        }

        // --- [기능 테스트] ---

        // F2: 목표 아이템 소환 (상점 구매 대용)
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            SpawnItemAtCamera(targetItemID);
        }

        // F3: 특정 ID 퀘스트 강제 완료 (기믹 통과 대용)
        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            QuestManager.Instance.NotifyCustomQuestMet(targetQuestID, NetworkManager.ServerClientId);
            Debug.Log($"<color=cyan>[DEBUG]</color> 퀘스트 {targetQuestID} 강제 클리어 보고.");
        }

        // F8: 다음 날 시뮬레이션 (모든 퀘스트 풀 새로고침)
        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            QuestManager.Instance.RefreshDailyQuestPoolsServerRpc();
            Debug.Log("<color=orange>[DEBUG]</color> 다음 날로 전환: 신규 3+1 풀 생성됨.");
        }

        // F4: 현재 상태 출력
        if (Keyboard.current.f4Key.wasPressedThisFrame)
        {
            PrintQuestStatus();
        }

        // F5: 자금 추가
        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            if (GameMaster.Instance != null && GameMaster.Instance.economyManager != null)
            {
                GameMaster.Instance.economyManager.availableLoanLimit.Value += 1000;
                Debug.Log($"<color=green>[DEBUG]</color> 자금 1000 추가.");
            }
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
            Debug.Log($"<color=orange>[DEBUG]</color> 아이템 {itemID}번 소환.");
        }
    }

    private void PrintQuestStatus()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b><color=white>=== 퀘스트 시스템 실시간 디버그 ===</color></b>");

        sb.Append("<b>[오늘의 제안]</b> ");
        sb.Append($"E:{QuestManager.Instance.easyOffered.Count}개, ");
        sb.Append($"N:{QuestManager.Instance.normalOffered.Count}개, ");
        sb.Append($"H:{QuestManager.Instance.hardOffered.Count}개\n");

        sb.Append("<b>[수락된 퀘스트]</b> ");
        foreach (var id in QuestManager.Instance.activeQuests) sb.Append($"[{id}] ");
        sb.AppendLine();

        sb.Append("<b>[클리어 기록]</b> ");
        foreach (var id in QuestManager.Instance.serverCompletedQuests) sb.Append($"<color=green>[{id}]</color> ");
        sb.AppendLine();

        int reward = QuestManager.Instance.GetCalculatedQuestReward();
        sb.AppendLine($"<b>[예상 보너스]</b> <color=yellow>{reward}원</color>");

        Debug.Log(sb.ToString());
    }
}