using Unity.Cinemachine;
using UnityEngine;

public class CinemachineController : MonoBehaviour
{
    public static CinemachineController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<CinemachineController>();
            }
            return _instance;
        }
    }
    static CinemachineController _instance;

    [Header("카메라 리스트")]
    public CinemachineCamera mainVcam;
    public CinemachineCamera monsterVcam;

    void Awake()
    {
        if (_instance == null) _instance = this;
        
        SetMainCameraActive();
    }

    // [추가] 몬스터가 스폰되었을 때 호출하여 카메라를 등록하는 함수
    public void RegisterMonsterCamera(CinemachineCamera vcam)
    {
        monsterVcam = vcam;
        monsterVcam.Priority = 5; // 기본적으로는 낮은 우선순위
        Debug.Log($"몬스터 카메라 등록 완료: {vcam.gameObject.name}");
    }

    public void SetMainCameraActive()
    {
        if (mainVcam != null) mainVcam.Priority = 10;
        if (monsterVcam != null) monsterVcam.Priority = 5;
    }

    public void SetMonsterCameraActive()
    {
        // 만약 아직 할당이 안 되었다면 씬에서 이름으로 찾아봄 (보험용)
        if (monsterVcam == null)
        {
            TryFindMonsterCamera();
        }

        if (monsterVcam != null)
        {
            mainVcam.Priority = 5;
            monsterVcam.Priority = 10;
        }
    }

    // 씬에서 Root_Hand 몬스터의 자식 카메라를 찾는 로직
    void TryFindMonsterCamera()
    {
        // 1. FaceMonster 레이어 인덱스 가져오기
        int monsterLayer = LayerMask.NameToLayer("FaceMonster");
        if (monsterLayer == -1) return;

        // 2. 씬에 있는 모든 오브젝트 중 FaceMonster 레이어를 가진 것들 검색
        // (성능을 위해 FindObjectsByType보다 효율적인 방식 선택)
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == monsterLayer)
            {
                // 해당 몬스터의 자식에서 시네머신 카메라를 찾음
                CinemachineCamera cam = obj.GetComponentInChildren<CinemachineCamera>();
                if (cam != null)
                {
                    RegisterMonsterCamera(cam);
                    return; // 찾았으면 종료
                }
            }
        }
    }
}