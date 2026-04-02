using UnityEngine;

public class NetworkGlobalSettings : MonoBehaviour
{
    public static NetworkGlobalSettings Instance;

    [Header("Mode Switch")]
    [Tooltip("체크하면 멀티플레이 모드로 동작합니다.")]
    public bool isMultiplayerMode = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}