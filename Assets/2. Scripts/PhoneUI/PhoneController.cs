using UnityEngine;
using UnityEngine.SceneManagement;

public class AutoSceneUIController : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private string titleSceneName = "KJY_TITLE"; // 타이틀 씬 이름

    // 이 오브젝트의 실제 비주얼 요소 (스크립트가 꺼지는 걸 방지하기 위해 자식 오브젝트를 끄는 걸 추천)
    [SerializeField] private GameObject visualChild;

    private void Awake()
    {
        // 씬 전환 시 오브젝트가 파괴되지 않게 유지해야 로직이 계속 돌아갑니다.
        // 만약 이미 부모가 DontDestroyOnLoad라면 이 줄은 생략 가능합니다.
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // 씬 로드 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 현재 로드된 씬 이름 확인
        if (scene.name == titleSceneName)
        {
            // 타이틀 씬이면 비활성화
            SetUIActive(false);
        }
        else
        {
            // 그 외(Lobby, Game 등) 씬이면 활성화
            SetUIActive(true);
        }
    }

    private void SetUIActive(bool isActive)
    {
        // 1. 만약 visualChild를 따로 등록했다면 그것만 끕니다. (권장)
        if (visualChild != null)
        {
            visualChild.SetActive(isActive);
        }
        // 2. 따로 등록 안 했다면 오브젝트의 Renderer나 Canvas 컴포넌트만 끕니다.
        // gameObject.SetActive(false)를 하면 스크립트 자체가 멈춰서 다음 씬에서 못 깨어납니다.
        else
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = isActive;

            // 혹은 하이어라키 상의 모든 자식을 끄는 방법
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(isActive);
            }
        }
    }
}