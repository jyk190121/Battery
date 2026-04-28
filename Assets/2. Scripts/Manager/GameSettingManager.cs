using UnityEngine;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance;

    private const string MouseSensitivityKey = "MouseSensitivity";
    public float CurrentSensitivity { get; private set; } = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings(); // 저장된 값 불러오기
        }
        else { Destroy(gameObject); }
    }

    public void SetMouseSensitivity(float value)
    {
        CurrentSensitivity = value;
        PlayerPrefs.SetFloat(MouseSensitivityKey, value);
        PlayerPrefs.Save();

        // 현재 씬에 이미 스폰된 내 플레이어가 있다면 즉시 적용
        ApplyToLocalPlayer();
    }

    private void LoadSettings()
    {
        CurrentSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, 1.0f);
    }

    public void ApplyToLocalPlayer()
    {
        // 씬 내의 모든 PlayerRotation 중 IsOwner인 것만 찾아 적용
        var players = FindObjectsByType<PlayerRotation>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.IsOwner)
            {
                p.mouseSensitivityMultiplier = CurrentSensitivity;
            }
        }
    }
}