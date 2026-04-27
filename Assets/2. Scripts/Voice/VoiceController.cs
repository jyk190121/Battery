using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class VoiceController : MonoBehaviour
{
    [Header("오디오 믹서 설정")]
    public AudioMixerGroup defaultMixer; // 일반 3D 환경음 (생목소리)
    public AudioMixerGroup phoneMixer;   // 전화기 필터 적용 (통화 소리)

    [Header("거리 임계값 설정 (Inspector에서 수정 가능)")]
    public float horizontalTransitionDistance = 15f; // X, Z 수평 거리 기준 
    public float verticalFloorLimit = 10f;          // Y축(높이) 차이 허용치 (층간 구분)

    [Range(0f, 1f)]
    public float highFidelityThreshold = 0.4f;      // 수평 거리의 몇 % 안으로 들어왔을 때 생목소리로 바꿀지

    private AudioSource audioSource;
    private Transform listener;
    private bool isCalling = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // 기본 오디오 설정 초기화
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 25f;

        if (Camera.main != null) listener = Camera.main.transform;
    }

    private void Update()
    {
        if (audioSource == null) return;

        if (listener == null)
        {
            if (Camera.main != null)
            {
                listener = Camera.main.transform;
            }
            else return;
        }
        

        // 1. 전화 중이 아닐 때: 무조건 3D 사운드 (V키를 누른 소리만 들림)
        if (!isCalling)
        {
            audioSource.spatialBlend = 1.0f;
            if (audioSource.outputAudioMixerGroup != defaultMixer)
                audioSource.outputAudioMixerGroup = defaultMixer;
            return;
        }

        // 2. 전화 중일 때: X, Z 평면 거리와 Y 높이를 각각 계산
        Vector3 playerPos = transform.position;
        Vector3 listenerPos = listener.position;

        // Y축 차이 (절대값)
        float diffY = Mathf.Abs(playerPos.y - listenerPos.y);

        // X, Z 수평 거리 계산 (피타고라스 정리)
        float diffX = playerPos.x - listenerPos.x;
        float diffZ = playerPos.z - listenerPos.z;
        float horizontalDistance = Mathf.Sqrt(diffX * diffX + diffZ * diffZ);

        // 조건 검사: 층이 아예 다르거나(Y > 10) 수평 거리가 너무 멀면 -> 완전한 2D 통화 모드
        if (diffY > verticalFloorLimit || horizontalDistance >= horizontalTransitionDistance)
        {
            audioSource.spatialBlend = 0.0f;
            if (audioSource.outputAudioMixerGroup != phoneMixer)
                audioSource.outputAudioMixerGroup = phoneMixer;
        }
        // 같은 층 범위 내에 있고 수평으로 가까워질 때 -> 3D와 2D를 블렌딩
        else
        {
            // 가까울수록 1.0(3D)에 가까워지는 값 계산
            float blendFactor = 1.0f - (horizontalDistance / horizontalTransitionDistance);
            audioSource.spatialBlend = blendFactor;

            // 특정 거리(예: 15m의 40%인 6m) 안쪽이면 생목소리 믹서로 전환
            if (horizontalDistance < horizontalTransitionDistance * highFidelityThreshold)
            {
                if (audioSource.outputAudioMixerGroup != defaultMixer)
                    audioSource.outputAudioMixerGroup = defaultMixer;
            }
            else
            {
                if (audioSource.outputAudioMixerGroup != phoneMixer)
                    audioSource.outputAudioMixerGroup = phoneMixer;
            }
        }
    }

    public void SetCallMode(bool state)
    {
        isCalling = state;
    }
}