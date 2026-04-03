using UnityEngine;

public class NetworkGlobalSettings : MonoBehaviour
{
    private static NetworkGlobalSettings _instance;

    public static NetworkGlobalSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에서 찾지 못했을 때를 대비해 경고창을 띄워 실수를 방지합니다.
                _instance = Object.FindFirstObjectByType<NetworkGlobalSettings>();
                if (_instance == null)
                {
                    Debug.LogWarning("<color=red><b>[System]</b></color> 씬에 NetworkGlobalSettings가 없습니다! 기본값(싱글)으로 동작합니다.");
                }
            }
            return _instance;
        }
    }

    [Header("Mode Switch")]
    [Tooltip("체크하면 멀티플레이(Netcode) 모드로 동작합니다.")]
    public bool isMultiplayerMode = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    //  멀티플레이 여부를 판단하는 전용 프로퍼티 (가독성 향상)
    public bool IsMultiplayer => isMultiplayerMode;
}