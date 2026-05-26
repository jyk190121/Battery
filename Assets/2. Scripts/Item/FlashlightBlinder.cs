using UnityEngine;
using Unity.Netcode;

public class FlashlightBlinder : NetworkBehaviour
{
    private CanvasGroup blindCanvasGroup; // 인스펙터 할당 대신 스크립트로 직접 찾음

    [Header("조건 설정")]
    public float maxBlindDistance = 15f; // 눈뽕이 닿는 최대 거리
    public float maxViewAngle = 60f;     // 내가 빛을 바라보는 허용 시야각
    public LayerMask obstacleLayer;      // 벽/장애물 레이어 (Default 등 체크)

    void Update()
    {
        // 💡 1. 오직 내 화면(로컬 플레이어)에서만 계산합니다.
        if (!IsOwner) return;

        // 💡 2. UI 직접 찾기 (FlashEffect.cs와 동일한 로직)
        if (blindCanvasGroup == null || blindCanvasGroup.gameObject == null)
        {
            if (SceneUIReference.Instance != null && SceneUIReference.Instance.blindImage != null)
            {
                blindCanvasGroup = SceneUIReference.Instance.blindImage.GetComponent<CanvasGroup>();
            }
            else
            {
                // 최악의 경우 직접 찾기
                GameObject go = GameObject.Find("Img_Blind");
                if (go != null) blindCanvasGroup = go.GetComponent<CanvasGroup>();
            }

            // 타이틀 씬 등 캔버스가 아예 없는 씬이라면 이번 프레임 패스
            if (blindCanvasGroup == null) return;
        }

        // 💡 3. 손전등 눈뽕 수학적 계산
        float maxBlindAlpha = 0f;

        // 이전 답변에서 Item_Flash에 추가했던 전역 리스트(AllFlashes)를 순회합니다.
        foreach (var flash in Item_Flash.AllFlashes)
        {
            // 내 손전등이거나, 켜져있지 않다면 무시
            if (flash == null || flash.IsOwner || flash.spotLight == null || !flash.spotLight.enabled)
                continue;

            Transform lightTrans = flash.spotLight.transform;
            Transform myCam = Camera.main != null ? Camera.main.transform : null;
            if (myCam == null) continue;

            // 거리 체크
            float dist = Vector3.Distance(lightTrans.position, myCam.position);
            if (dist > maxBlindDistance) continue;

            // 빛의 원뿔 반경 안에 내 카메라가 들어왔는지 체크
            Vector3 dirToMe = (myCam.position - lightTrans.position).normalized;
            float angleFromLight = Vector3.Angle(lightTrans.forward, dirToMe);
            if (angleFromLight > flash.spotLight.spotAngle / 2f) continue;

            // 내가 손전등을 쳐다보고 있는지 체크
            float myLookAngle = Vector3.Angle(myCam.forward, -dirToMe);
            if (myLookAngle > maxViewAngle) continue;

            // 벽 너머인지 Raycast 체크
            if (Physics.Raycast(lightTrans.position, dirToMe, dist, obstacleLayer)) continue;

            // 눈뽕 강도 계산 (가까울수록, 정면으로 볼수록 하얗게)
            float distanceFactor = 1f - (dist / maxBlindDistance);
            float angleFactor = 1f - (myLookAngle / maxViewAngle);

            float alpha = distanceFactor * angleFactor;
            if (alpha > maxBlindAlpha) maxBlindAlpha = alpha;
        }

        // 💡 4. 찾아낸 CanvasGroup에 부드럽게 투명도 적용
        // 손전등이 나를 비추면 하얗게 변하고, 고개를 돌리면 자연스럽게 원래대로 돌아옵니다.
        blindCanvasGroup.alpha = Mathf.Lerp(blindCanvasGroup.alpha, maxBlindAlpha, Time.deltaTime * 10f);
    }
}