using UnityEngine;
using Unity.Netcode;

public class CollectionSafeGimmick : NetworkBehaviour
{
    [Header("연동 필요")]
    public Transform internalDropPoint; // 팀원분이 만들어둔 금고 안쪽 빈 오브젝트

    public override void OnNetworkSpawn()
    {
        // 1. 방장(서버) 컴퓨터에서만 퀘스트 여부를 판단합니다.
        if (IsServer)
        {
            // 2. 마법의 한 줄: 퀘스트 매니저에게 스폰을 요청하고 결과(기믹 활성화 여부)를 받음
            bool isQuestActive = QuestManager.Instance.TrySetupSafeGimmick(internalDropPoint);

            // 3. 결과에 따라 금고 상태 세팅
            if (isQuestActive)
            {
                // 이번 판에 금고 퀘스트가 걸렸습니다! (아이템은 이미 금고 안에 스폰됨)
                Debug.Log("금고 퀘스트 활성화! 비밀번호 기믹을 시작합니다.");

                // TODO: 금고 문 잠그기, 비밀번호 칠판 랜덤 생성 등 팀원분 기믹 로직 실행
                // ActivateSafe(); 
            }
            else
            {
                // 이번 판은 금고 퀘스트가 없습니다.
                Debug.Log("금고 퀘스트 없음. 금고를 비활성화합니다.");

                // TODO: 금고 문을 그냥 열어두거나 상호작용 자체를 꺼버리는 로직 실행
                // DisableSafe(); 
            }
        }
    }
}