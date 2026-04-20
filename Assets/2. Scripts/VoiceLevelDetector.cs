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

    // 외부(UI 등)에서 현재 마이크 볼륨을 시각적으로 띄워줄 때 읽어갈 수 있는 값
    public float CurrentLevel { get; private set; }

    private PlayerController _localPlayer;
    private float _lastReportTime;


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
        // 1. 내 캐릭터가 아직 스폰되지 않았다면 찾기를 시도합니다. (로딩 중 예외 처리)
        if (_localPlayer == null)
        {
            FindLocalPlayer();
            return;
        }

        // 2. 리코더가 정상 작동 중이고, 마이크가 켜져서 음성을 송출 중일 때만 체크
        if (recorder != null && recorder.TransmitEnabled)
        {
            // Photon Voice 리코더에서 제공하는 레벨 미터(0~1 사이의 피크 볼륨)
            CurrentLevel = recorder.LevelMeter.CurrentPeakAmp;

            // 3. 현재 볼륨이 설정한 임계치(잡음)보다 크고, 쿨타임이 돌았을 때만 서버로 보고!
            if (CurrentLevel > voiceThreshold && Time.time - _lastReportTime >= reportCooldown)
            {
                // 소음 레벨 계산 (순수하게 임계치를 넘긴 볼륨 * 민감도)
                float noiseLevel = (CurrentLevel - voiceThreshold) * voiceSensitivity;

                // 내 플레이어 컨트롤러를 통해 서버에 "나 여기서 소리 냈어!" 라고 알림
                _localPlayer.ReportNoiseServerRpc(_localPlayer.transform.position, noiseLevel);

                // 쿨타임 타이머 리셋
                _lastReportTime = Time.time;

                Debug.Log($"<color=white>[Mic]</color> 소음 발생! 서버로 전달함. (레벨: {noiseLevel:F1})");
            }
        }
        else
        {
            // 마이크를 껐거나 소리를 안 내고 있다면 0으로 고정
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