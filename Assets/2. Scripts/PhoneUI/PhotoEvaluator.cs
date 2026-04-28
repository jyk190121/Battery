using UnityEngine;
using System.Collections.Generic;

public class PhotoTarget : MonoBehaviour
{
    public string targetIdentifier; // 예: "RedCar", "BlueTree", "Player1"
    public int baseScore = 10;
}

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
            satisfiedQuestIDs = new List<int>()
        };

        // 1. 카메라 시야각 계산
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(captureCam);

        // 2. 씬 내의 판정 대상 탐색
        Collider[] targetInRadius = Physics.OverlapSphere(captureCam.transform.position, maxCaptureDistance, targetLayer);

        int playerCount = 0;

        foreach(Collider target in targetInRadius)
        {
            // 3. 시야각 내에 존재하는가? (Frustum Check)
            if (GeometryUtility.TestPlanesAABB(planes, target.bounds))
            {
                // 4. 벽에 가려지지 않았는가? (Raycast Check - 사실적인 현장감 부여)
                Vector3 directionToTarget = target.bounds.center - captureCam.transform.position;

                bool isBlocked = Physics.Raycast(captureCam.transform.position, directionToTarget.normalized, directionToTarget.magnitude, obstacleLayer);

                Color rayColor = isBlocked ? Color.red : Color.green;
                Debug.DrawRay(captureCam.transform.position, directionToTarget, rayColor, 3f);
                // ==========================================
                if (!Physics.Raycast(captureCam.transform.position, directionToTarget.normalized, directionToTarget.magnitude, obstacleLayer))
                {
                    // 콜라이더에서 PhotoTarget 컴포넌트를 뽑아냄
                    PhotoTarget pTarget = target.GetComponent<PhotoTarget>();
                    if (pTarget != null)
                    {
                        // 찰칵! 사진 데이터에 이 피사체의 이름을 기록함
                        newData.capturedTargets.Add(pTarget.targetIdentifier);
                    }
                }
            }
        }

        newData.playersInFrame = playerCount;

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
