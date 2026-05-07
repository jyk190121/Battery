using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 특정 씬(학교 등)의 배경음(환경음)을 관리하는 로컬 매니저입니다.
/// </summary>
public class AmbientSoundManager : MonoBehaviour
{
    [Header("--- Ambient Clips ---")]
    [Tooltip("실외에서 들리는 선명한 빗소리")]
    public AudioClip rainOutdoorClip;

    [Header("--- Audio Sources ---")]
    [SerializeField] private AudioSource rainSource; // Loop 설정된 오디오 소스

    private void Start()
    {
        // 1. 빗소리 재생 시작
        if (rainOutdoorClip != null)
        {
            rainSource.clip = rainOutdoorClip;
            rainSource.loop = true;
            rainSource.Play();
        }

        // 2. 시작 시 플레이어 위치 확인하여 초기 스냅샷 설정
        CheckInitialSnapshot();
    }

    private void CheckInitialSnapshot()
    {
        // 게임 시작 시 플레이어가 이미 안에 있다면 먹먹한 소리로 시작
        if (PlayerController.LocalPlayer != null)
        {
            if (PlayerController.LocalPlayer.isInsideFacility.Value)
            {
                SoundManager.Instance.SetIndoorSnapshot(true, 0.01f);
            }
            else
            {
                SoundManager.Instance.SetIndoorSnapshot(false, 0.01f);
            }
        }
    }
}