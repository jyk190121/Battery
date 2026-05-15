using UnityEngine;
using System.Collections.Generic;

public class PhotoEvaluator : MonoBehaviour
{
    public static PhotoEvaluator Instance;

    [Header("Evaluation Settings")]
    public LayerMask obstacleLayer;
    public LayerMask targetLayer;
    public float maxCaptureDistance = 10f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public PhotoData EvaluateCapture(Camera captureCam, Texture2D capturedImage)
    {
        PhotoData newData = new PhotoData()
        {
            image = capturedImage,
            satisfiedQuestIDs = new List<int>(),
            capturedTargets = new List<string>()
        };

        // 1. 카메라 시야각 계산
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(captureCam);

        // 2. 씬 내의 판정 대상 탐색
        Collider[] targetInRadius = Physics.OverlapSphere(captureCam.transform.position, maxCaptureDistance, targetLayer);

        //int playerCount = 0;

        foreach (Collider target in targetInRadius)
        {
            Debug.Log($"<color=yellow>[센서 감지]</color> 반경 내 콜라이더 발견: {target.name}");

            // 3. 시야각 내에 존재하는가? (Frustum Check)
            if (GeometryUtility.TestPlanesAABB(planes, target.bounds))
            {
                // 4. 벽에 가려지지 않았는가? (Raycast Check)
                Vector3 directionToTarget = target.bounds.center - captureCam.transform.position;
                float dist = directionToTarget.magnitude;

                // 무엇에 막혔는지 정확히 알기 위해 out RaycastHit 사용
                bool isBlocked = Physics.Raycast(captureCam.transform.position, directionToTarget.normalized, out RaycastHit hit, dist, obstacleLayer);

                if (isBlocked)
                {
                    Debug.Log($"<color=red>[장애물 막힘]</color> {target.name} 타겟이 {hit.collider.name}(Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})에 가려져서 탈락!");
                }
                else
                {
                    PhotoTarget pTarget = target.GetComponent<PhotoTarget>();
                    if (pTarget != null)
                    {
                        Debug.Log($"<color=lime>[인식 성공!]</color> {target.name} ({pTarget.targetIdentifier}) 찰칵!");
                        if (!newData.capturedTargets.Contains(pTarget.targetIdentifier))
                        {
                            newData.capturedTargets.Add(pTarget.targetIdentifier);
                        }
                    }
                    else
                    {
                        Debug.Log($"<color=orange>[컴포넌트 누락]</color> {target.name}에 PhotoTarget 스크립트가 없습니다!");
                    }
                }
            }
            else
            {
                Debug.Log($"<color=grey>[화각 이탈]</color> {target.name}이(가) 카메라 렌즈 화각(Frustum) 밖입니다.");
            }
        }
        //// 1차 판독: 오늘 수락한 촬영 퀘스트 중 타겟이 찍힌 것을 찾아 임시 보관
        //if (QuestManager.Instance != null)
        //{
        //    foreach (int qId in QuestManager.Instance.activeQuests)
        //    {
        //        QuestDataSO questData = QuestManager.Instance.GetQuestData(qId);
        //        if (questData != null && questData.type == QuestType.Photo)
        //        {
        //            if (newData.capturedTargets.Contains(questData.targetType) && !newData.satisfiedQuestIDs.Contains(qId))
        //            {
        //                newData.satisfiedQuestIDs.Add(qId);
        //            }
        //        }
        //    }
        //}
        // 블랙박스 해체 디버그 로그
        Debug.Log($"<color=cyan>[디버그1-물리센서]</color> 사진에 찍힌 명찰 목록: {(newData.capturedTargets.Count > 0 ? string.Join(", ", newData.capturedTargets) : "아무것도 안찍힘!")}");
        //Debug.Log($"<color=cyan>[디버그2-1차합격]</color> 'Monster'가 찍혀서 1차 합격한 퀘스트 수: {newData.satisfiedQuestIDs.Count}");

        ////  30번대 퀘스트 난이도별 복합 조건 2차 판정
        //QuestCameraBridge.ValidatePhotoData(newData);

        //Debug.Log($"<color=cyan>[디버그3-최종심사]</color> 노말/하드 추가 조건(Player/Item) 검사 후 최종 생존 퀘스트 수: {newData.satisfiedQuestIDs.Count}");

        //// 최종 판독을 통과한 퀘스트만 개인 장부에 보고
        //foreach (int finalId in newData.satisfiedQuestIDs)
        //{
        //    if (QuestCameraBridge.Instance != null)
        //    {
        //        QuestCameraBridge.Instance.AddCapturedQuest(finalId);
        //    }
        //}

        return newData;
    }

    private void OnDrawGizmos()
    {
        // 캡처 카메라가 어디있는지 찾아서 그 주변으로 원을 그림 (에디터 실행 중에만 작동)
        if (Application.isPlaying)
        {
            CameraConnect camConnect = FindAnyObjectByType<CameraConnect>();
            if (camConnect != null && camConnect.CaptureCamera != null)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f); // 반투명한 초록색
                Gizmos.DrawWireSphere(camConnect.CaptureCamera.transform.position, maxCaptureDistance);
            }
        }
    }
}
