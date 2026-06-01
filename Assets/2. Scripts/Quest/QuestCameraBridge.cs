using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

/// <summary>
/// 스마트폰 카메라로 촬영한 사진 데이터를 판독하고, 정산 시 서버로 전송하는 로컬 앨범 브릿지.
/// </summary>
public class QuestCameraBridge : NetworkBehaviour
{
    public static QuestCameraBridge Instance;

    // 찍었지만 서버는 모르는 상태 (개인 앨범) -> 이후 제출.
    private List<int> myLocalDeferredQuests = new List<int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public void AddCapturedQuest(int questID)
    {
        if (!myLocalDeferredQuests.Contains(questID))
        {
            myLocalDeferredQuests.Add(questID);
            Debug.Log($"<color=orange>[스마트폰 앨범]</color> {questID}번 데이터 확보완료. (정산 시 인정됨)");
        }
    }

    public void DeletePhotoFromAlbum(int questID)
    {
        if (myLocalDeferredQuests.Contains(questID))
        {
            myLocalDeferredQuests.Remove(questID);
            Debug.Log($"<color=red>[스마트폰 앨범]</color> {questID}번 데이터 삭제됨.");
        }
    }

    [Rpc(SendTo.Everyone)]
    public void CommandSubmitDataClientRpc(ulong[] survivorIds)
    {
        ulong myClientId = NetworkManager.Singleton.LocalClientId;

        if (survivorIds.Contains(myClientId) && myLocalDeferredQuests.Count > 0)
        {
            SubmitDeferredDataServerRpc(myLocalDeferredQuests.ToArray());
        }

        myLocalDeferredQuests.Clear();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitDeferredDataServerRpc(int[] questIDs, RpcParams rpcParams = default)
    {
        ulong actualSenderId = rpcParams.Receive.SenderClientId;

        foreach (int questID in questIDs)
        {
            QuestManager.Instance.NotifyFinalClear(questID, actualSenderId);
        }
    }

    public bool IsPhotoInLocalAlbum(int questID)
    {
        return myLocalDeferredQuests.Contains(questID);
    }

    public void RecalculateLocalDeferredQuests()
    {
        myLocalDeferredQuests.Clear();

        foreach (PhotoData photoData in PhotoDataManager.Instance.currentPhotos)
        {
            foreach (int questID in photoData.satisfiedQuestIDs)
            {
                if (!myLocalDeferredQuests.Contains(questID))
                {
                    myLocalDeferredQuests.Add(questID);
                }
            }
        }

        Debug.Log($"<color=orange>[스마트폰 앨범 상태 갱신]</color> 현재 제출 대기 중인 퀘스트 개수: {myLocalDeferredQuests.Count}");
    }

    public static void ValidatePhotoData(PhotoData data)
    {
        if (QuestManager.Instance == null || data.satisfiedQuestIDs.Count == 0)
        {
            return;
        }

        List<int> invalidQuestIDsToRemove = new List<int>();

        foreach (int questID in data.satisfiedQuestIDs)
        {
            // 사진 퀘스트(30번대)가 아니면 검사 패스
            if (questID % 100 != 30)
            {
                continue;
            }

            QuestDataSO questData = QuestManager.Instance.GetQuestData(questID);

            if (questData == null)
            {
                continue;
            }

            if (questData.difficulty == QuestDifficulty.Easy)
            {
                continue;
            }
            else if (questData.difficulty == QuestDifficulty.Normal)
            {
                // 노말: 몬스터 + 플레이어 동시 촬영 필요
                if (!data.capturedTargets.Contains("Player"))
                {
                    invalidQuestIDsToRemove.Add(questID);
                }
            }
            else if (questData.difficulty == QuestDifficulty.Hard)
            {
                // 하드: 몬스터 + 특정 아이템 동시 촬영 필요
                if (!data.capturedTargets.Contains("Item"))
                {
                    invalidQuestIDsToRemove.Add(questID);
                }
            }
        }

        foreach (int removeID in invalidQuestIDsToRemove)
        {
            data.satisfiedQuestIDs.Remove(removeID);
        }
    }
}