using UnityEngine;
using Photon.Voice.Unity;
using Unity.Netcode;

/// <summary>
/// 로컬 플레이어의 마이크 볼륨을 실시간으로 측정하고,
/// 일정 크기 이상의 소음이 발생하면 서버(몬스터)에 알리는 시스템입니다.
/// </summary>
public class VoiceLevelDetector : MonoBehaviour
{
    // =========================================================
    // 1. 변수 선언부
    // =========================================================

    public static VoiceLevelDetector Instance;

    [Header("--- References ---")]
    [Tooltip("Photon Voice의 마이크 입력을 담당하는 리코더")]
    public Recorder recorder;

    [Header("--- Noise Settings ---")]
    [Tooltip("소음으로 간주할 최소 볼륨 임계치 (0.01 ~ 0.1 추천, 주변 잡음 필터링용)")]
    public float voiceThreshold = 0.02f;

    [Tooltip("마이크 볼륨에 곱할 가중치 (값이 클수록 소리가 멀리까지 퍼짐)")]
    public float voiceSensitivity = 100f;

    [Tooltip("서버 과부하 방지용 RPC 전송 쿨타임 (초)")]
    public float reportCooldown = 0.5f;

    [Header("--- Smoothing Settings ---")]
    [Tooltip("값이 클수록 실시간 반응이 빠르고, 작을수록 더 부드럽게 평균화합니다. (5~15 추천)")]
    public float smoothingSpeed = 10f;

    // 외부(UI 등)에서 현재 마이크 볼륨을 시각적으로 띄워줄 때 읽어갈 수 있는 값
    public float CurrentLevel { get; private set; }

    private PlayerController _localPlayer;
    private float _lastReportTime;
    private float _smoothedLevel;


    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // 리코더가 안 들어와 있다면 자동으로 찾아서 연결
        if (recorder == null) recorder = GetComponent<Recorder>();
    }


    // =========================================================
    // 3. 유니티 루프 
    // =========================================================

    /// <summary>
    /// 매 프레임 마이크 볼륨을 체크하고, 서버로 전송할지 결정합니다.
    /// </summary>
    private void Update()
    {
        if (_localPlayer == null)
        {
            FindLocalPlayer();
            return;
        }

        if (recorder != null && recorder.TransmitEnabled)
        {
            // 1. 현재 프레임의 가공되지 않은 피크 값 가져오기
            float rawLevel = recorder.LevelMeter.CurrentPeakAmp;

            // 2. 보간법(Lerp)을 이용해 부드러운 평균값 계산
            // 이전 값과 현재 값을 섞어서 급격한 변화를 억제합니다.
            _smoothedLevel = Mathf.Lerp(_smoothedLevel, rawLevel, Time.deltaTime * smoothingSpeed);

            CurrentLevel = _smoothedLevel;

            // 3. 평균화된 값이 임계치를 넘었는지 확인
            if (CurrentLevel > voiceThreshold && Time.time - _lastReportTime >= reportCooldown)
            {
                float noiseLevel = (CurrentLevel - voiceThreshold) * voiceSensitivity;

                _localPlayer.ReportNoiseServerRpc(_localPlayer.transform.position, noiseLevel, _localPlayer.isInsideFacility.Value);

                _lastReportTime = Time.time;
                Debug.Log($"<color=cyan>[Mic]</color> 평균 소음 감지! (레벨: {noiseLevel:F1})");
            }
        }
        else
        {
            _smoothedLevel = 0f;
            CurrentLevel = 0f;
        }
    }


    // =========================================================
    // 4. 퍼블릭 함수
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// Netcode의 NetworkManager를 통해 현재 내가 조종하고 있는 캐릭터 오브젝트를 찾습니다.
    /// </summary>
    private void FindLocalPlayer()
    {
        // 서버/클라이언트가 완전히 연결되었고, 내 클라이언트 객체가 생성되었는지 확인
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localObj = NetworkManager.Singleton.LocalClient.PlayerObject;

            if (localObj != null)
            {
                _localPlayer = localObj.GetComponent<PlayerController>();
            }
        }
    }
}