using UnityEngine;
using Unity.Netcode;

public class SafeQuestTester : NetworkBehaviour
{
    [Header("테스트용 스폰 위치")]
    public Transform dropPoint;

    public override void OnNetworkSpawn()
    {
        // 씬 로드 직후 서버(방장) 측에서만 테스트 실행
        if (IsServer)
        {
            // QuestManager 초기화 및 퀘스트 데이터 동기화를 위해 약간의 지연 후 실행
            Invoke(nameof(RunTest), 1.0f);
        }
    }

    private void RunTest()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError("[Tester] QuestManager가 존재하지 않습니다.");
            return;
        }

        // 실제 팀원분이 호출할 API 테스트
        bool isQuestActive = QuestManager.Instance.TrySetupSafeGimmick(dropPoint);
        Debug.Log($"[Tester] 금고 기믹 활성화 및 스폰 결과: {isQuestActive}");
    }
}