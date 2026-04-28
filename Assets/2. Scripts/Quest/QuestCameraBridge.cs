using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class QuestCameraBridge : NetworkBehaviour
{
    public static QuestCameraBridge Instance;

    //찍었지만 서버는 모르는 상태 (개인 앨범) -> 이후 제출.
    private List<int> myLocalDeferredQuests = new List<int>();

    private void Awake() => Instance = this;

    //퀘스트 목표 촬영시 호출할 함수
    public void AddCapturedQuest(int qId)
    {
        //이미 추가된 퀘스트 ID는 다시 추가안함.
        if (!myLocalDeferredQuests.Contains(qId))
        {
            myLocalDeferredQuests.Add(qId);//로컬에 보관하면서

            Debug.Log($"<color=orange>[스마트폰 앨범]</color> {qId}번 데이터 확보완료. (정산 시 인정됨)");
        }
    }

    //유저가 스마트폰에서 사진을 직접 삭제했을 때 (체크 해제)
  public void DeletePhotoFromAlbum(int questID)
    {
        if (myLocalDeferredQuests.Contains(questID))
        {
            myLocalDeferredQuests.Remove(questID);
            Debug.Log($"<color=red>[스마트폰 앨범]</color> {questID}번 데이터 삭제됨.");
        }
    }



    //정산존 명령 수신 (모든 클라이언트가 동시 실행)
    [Rpc(SendTo.Everyone)]
    public void CommandSubmitDataClientRpc(ulong[] survivorIds)
    {
        ulong myId = NetworkManager.Singleton.LocalClientId;

        //각자 자기 ID가 생존자 명단에 있는지 스스로 확인
        if (survivorIds.Contains(myId) && myLocalDeferredQuests.Count > 0)
        {
            //내가 살아있다면 앨범 데이터를 서버로 전송!
            SubmitDeferredDataServerRpc(myLocalDeferredQuests.ToArray());
        }

        //확인이 끝났으면 살았든 죽었든 앨범은 초기화 (다음 날 준비 or 증발)
        myLocalDeferredQuests.Clear();
    }

    //서버 매니저에게 실제 데이터 제출 (기록 확정)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitDeferredDataServerRpc(int[] questIDs)
    {
        foreach (int id in questIDs)
        {
            //서버 매니저의 마스터 장부에 도장.
            QuestManager.Instance.NotifyFinalClear(id, NetworkManager.ServerClientId);
        }
    }

    //스마트폰 UI 판독기에서 앨범 상태를 물어볼 때 사용
    public bool IsPhotoInLocalAlbum(int id) => myLocalDeferredQuests.Contains(id);
}