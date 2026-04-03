using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }
    public float LoadingProgress { get; private set; }          // 로딩 진행률을 외부(UI)에서 읽을 수 있도록 공개
    private void Awake()
    {
        // 싱글톤 핵심 로직 수정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);                      // 이 객체를 씬이 바뀌어도 보존
        }
        else
        {
            Destroy(gameObject);                                // 중복 생성된 객체는 제거
        }
    }

    //void OnEnable()
    //{
    //    //씬 로드 완료 이벤트 구축
    //    SceneManager.sceneLoaded += OnSceneLoaded;
    //}

    //void OnDisable()
    //{
    //    //이벤트 구축 해제(메모리 누수 및 에러 방지)
    //    SceneManager.sceneLoaded -= OnSceneLoaded;
    //}

    //void InitializeBattle()
    //{
    //    //StageManager에서 넘겨준 스테이지 번호 확인
    //    StageManager stageManager = FindAnyObjectByType<StageManager>();
    //    int stage = stageManager.SelectedStage;
    //    int round = stageManager.Round;

    //    EnemyManager.Instance.SpawnEnemy(stage, round);
    //}

    //public void OnSceneLoaded(Scene scene, LoadSceneMode mode = LoadSceneMode.Single)
    //{
    //    if(scene.name == "BattleScene")
    //    {
    //        InitializeBattle();
    //    }
    //}

    // 기본 로드 방식 (동기)
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // 비동기 로드 방식 (추천: 로딩 화면 구현 시 유리)
    public void LoadSceneAsync(string sceneName)
    {
        //if (sceneName == "BattleScene")
        //{
        //    InitializeBattle();
        //}
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        // 1. 먼저 로딩 화면(정거장)으로 이동합니다.
        // 로딩 씬은 가벼우므로 동기 로딩해도 무방합니다.
        SceneManager.LoadScene("LoadingScene");
        yield return null;                      // 한 프레임 대기 (로딩 씬의 UI가 뜰 시간을 줌)

        // 2. 이제 실제 목표 씬을 비동기로 로드합니다.
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; // 90%에서 멈춰두기

        float startValue = LoadingProgress; // 현재 값 저장
        float fakeProgress = 0f;            // 연출용 변수

        //float timer = 0f;
        while (!op.isDone)
        {
            //// 진행률 계산 (0~1)
            //LoadingProgress = Mathf.Clamp01(op.progress / 0.9f);
            //print($"현재 씬 로드 진행률 : {LoadingProgress}");

            //// 로딩 완료 조건
            //if (op.progress >= 0.9f)
            //{
            //    yield return new WaitForSeconds(0.1f);

            //    // 연출을 위해 약간의 지연을 주거나 바로 전환
            //    op.allowSceneActivation = true;
            //}

            //yield return null;

            yield return null;

            // 1. 유니티의 실제 로딩 수치 (0.9가 최대)
            float realTarget = op.progress / 0.9f;

            // 2. 가짜 진행률을 실제 진행률을 따라가게 함 (부드럽게)
            // MoveTowards는 일정한 속도로 증가시켜서 Lerp보다 제어가 쉽습니다.
            fakeProgress = Mathf.MoveTowards(fakeProgress, realTarget, Time.unscaledDeltaTime * 0.5f);

            LoadingProgress = fakeProgress;

            // 3. 90% 완료 시 100%까지 강제 연출
            if (op.progress >= 0.9f)
            {
                fakeProgress = Mathf.MoveTowards(fakeProgress, 1f, Time.unscaledDeltaTime * 0.5f);
                LoadingProgress = fakeProgress;

                if (LoadingProgress >= 1f)
                {
                    op.allowSceneActivation = true;
                }
            }

            //Debug.Log($"보정된 로딩 진행률 : {LoadingProgress * 100}%");
        }
    }

    // 현재 씬 재시작
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public string SceneName()
    {
        return SceneManager.GetActiveScene().name;
    }
}