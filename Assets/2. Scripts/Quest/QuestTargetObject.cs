using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class QuestTargetObject : NetworkBehaviour
{
    [Header("Quest Settings")]
    public int relatedQuestID;       // 이 물체를 찍었을 때 완료되는 퀘스트 ID

    [Header("Record Settings (Optional)")]
    public string recordAnswer = "1234"; // 채록 퀘스트일 경우 카메라가 읽어갈 텍스트 단서

    // 사진기/카메라 로직을 담당하는 팀원이 셔터를 누를 때 이 함수를 호출해 주어야 함
    public void OnPhotographedBy(ulong photographerClientId)
    {
        QuestManager.Instance.NotifyCustomQuestMet(relatedQuestID, photographerClientId);
        Debug.Log($"<color=cyan>[Quest]</color> {relatedQuestID}번 퀘스트 사진/채록 완료.");
    }

    private void Update()
    {
        // 카메라 코드가 아직 없을 때 혼자 에디터에서 퀘스트를 테스트하기 위한 치트키 (F9)
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            if (IsSpawned && NetworkManager.Singleton.IsConnectedClient)
            {
                OnPhotographedBy(NetworkManager.Singleton.LocalClientId);
            }
        }
    }
}