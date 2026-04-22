using System.Collections;
using UnityEngine;

public class FlashEffect : MonoBehaviour
{
    private CanvasGroup flashCanvasGroup;
    private Coroutine flashRoutine;
   
    // 섬광탄 폭발 로직(ApplyEffect)에서 이 함수를 호출
    public void TriggerFlash(float duration = 3.0f)
    {
        // 1. 참조가 없다면 씬에서 이름으로 찾기
        if (flashCanvasGroup == null)
        {
            GameObject go = SceneUIReference.Instance.blindImage.gameObject;
            if (go != null)
            {
                flashCanvasGroup = go.GetComponent<CanvasGroup>();
            }
            else
            {
                Debug.LogError("<color=red>[Flashbang]</color> 씬에 'FlashbangOverlay' 오브젝트가 없습니다.");
                return;
            }
        }

        // 2. 기존 연출 중단 후 새로 시작
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashAlphaRoutine(duration));
    }

    private IEnumerator FlashAlphaRoutine(float duration)
    {
        // 시야 차단 시작 (Blocks Raycasts가 꺼져있어야 클릭이 통과됨)
        flashCanvasGroup.alpha = 1.0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (flashCanvasGroup == null) yield break;

            elapsed += Time.deltaTime;

            // 선형 보간으로 알파값 감소
            flashCanvasGroup.alpha = Mathf.Lerp(1.0f, 0.0f, elapsed / duration);

            yield return null;
        }

        flashCanvasGroup.alpha = 0f;
    }
}