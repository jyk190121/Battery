using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 몬스터의 모든 사운드를 전담하며, MonsterData(SO)에 등록된 클립을 사용합니다.
/// </summary>
public class MonsterSound : NetworkBehaviour
{
    [Header("참조")]
    public MonsterController owner;

    [Header("Audio Sources")]
    public AudioSource voiceSource;
    public AudioSource footstepSource;
    public AudioSource ambientSource;

    [Header("발소리 간격 설정")]
    public float walkStepInterval = 0.45f;
    public float runStepInterval = 0.3f;
    private float _stepTimer;

    // ==========================================
    // 오클루전 (벽 너머 먹먹함) 설정
    // ==========================================
    [Header("오클루전(장애물 차단) 설정")]
    [Tooltip("소리를 막을 장애물 레이어 (Wall, Floor, Default 등)")]
    public LayerMask obstacleMask;
    private AudioLowPassFilter _lowPassFilter;
    private float _targetFrequency = 22000f; // 22000 = 맑은 소리, 1500 = 먹먹한 소리
    private float _occlusionTimer;

    private AudioLowPassFilter _voiceFilter;
    private AudioLowPassFilter _footstepFilter;
    private AudioLowPassFilter _ambientFilter;

    public override void OnNetworkSpawn()
    {
        if (owner == null) owner = GetComponent<MonsterController>();

        _voiceFilter = SetupFilter(voiceSource);
        _footstepFilter = SetupFilter(footstepSource);
        _ambientFilter = SetupFilter(ambientSource);
    }

    void Update()
    {
        if (owner == null || owner.monsterData == null || ambientSource == null) return;

        // 1. 오클루전 연산 (모든 클라이언트가 각자 자기 귀(카메라) 기준으로 계산)
        HandleAudioOcclusion();

        MonsterStateType currentState = owner.CurrentStateNet.Value;

        // 2. 사망 상태면 모든 루프 사운드를 끔
        if (currentState == MonsterStateType.Dead)
        {
            if (ambientSource.isPlaying) ambientSource.Stop();
            return;
        }

        // 3. 상태에 따라 재생할 루프 클립 결정 (기본은 평소 숨소리)
        AudioClip targetLoopClip = owner.monsterData.breathClip;

        // 추격, 수색, 스토킹 중일 때는 거친 추격음(chaseClip)으로 변경
        if (currentState == MonsterStateType.Chase ||
            currentState == MonsterStateType.Investigate ||
            currentState == MonsterStateType.Stalk)
        {
            if (owner.monsterData.chaseClip != null)
            {
                targetLoopClip = owner.monsterData.chaseClip;
            }
        }

        // 4. 클립이 바뀌었으면 오디오 소스 교체 후 재생
        if (ambientSource.clip != targetLoopClip && targetLoopClip != null)
        {
            ambientSource.clip = targetLoopClip;
            ambientSource.loop = true;
            if (!ambientSource.isPlaying) ambientSource.Play();
        }

        // 5. 발소리 타이머 로직 (서버에서만 계산 후 방송)
        if (owner.IsServer)
        {
            if (owner.navAgent.enabled && owner.navAgent.velocity.sqrMagnitude > 0.1f)
            {
                float currentInterval = (currentState == MonsterStateType.Chase) ? runStepInterval : walkStepInterval;
                _stepTimer += Time.deltaTime;

                if (_stepTimer >= currentInterval)
                {
                    PlayFootstepClientRpc();
                    _stepTimer = 0f;
                }
            }
            else
            {
                _stepTimer = 0f;
            }
        }
    }

    private AudioLowPassFilter SetupFilter(AudioSource source)
    {
        if (source == null) return null;

        AudioLowPassFilter filter = source.GetComponent<AudioLowPassFilter>();
        if (filter == null)
        {
            filter = source.gameObject.AddComponent<AudioLowPassFilter>();
        }
        filter.cutoffFrequency = 22000f; // 시작할 때는 맑은 소리
        return filter;
    }

    // ==========================================
    // 사운드 먹먹함(Occlusion) 처리
    // ==========================================
    private void HandleAudioOcclusion()
    {
        // 매 프레임이 아닌 0.1초마다 한 번씩만 레이저를 쏩니다.
        _occlusionTimer += Time.deltaTime;
        if (_occlusionTimer >= 0.2f)
        {
            _occlusionTimer = 0f;

            if (Camera.main != null) // 로컬 플레이어의 귀(카메라)
            {
                Vector3 listenerPos = Camera.main.transform.position;
                Vector3 soundPos = transform.position + (Vector3.up * 1.5f); // 몬스터 가슴/입 높이
                Vector3 direction = listenerPos - soundPos;
                float distance = direction.magnitude;

                // 몬스터 -> 내 카메라 사이에 장애물(벽/바닥)이 있는지 레이캐스트 검사
                if (Physics.Raycast(soundPos, direction.normalized, distance, obstacleMask))
                {
                    // 벽에 막힘 -> 고음을 깎아내고 먹먹한 소리(1500Hz) 세팅
                    _targetFrequency = 1500f;
                }
                else
                {
                    // 뻥 뚫림 -> 맑은 원본 소리(22000Hz) 세팅
                    _targetFrequency = 22000f;
                }
            }
        }

        // 필터의 주파수를 목표치로 부드럽게(Lerp) 전환하여 갑자기 소리가 뚝 끊기는 느낌을 방지
        if (_lowPassFilter != null)
        {
            _lowPassFilter.cutoffFrequency = Mathf.Lerp(_lowPassFilter.cutoffFrequency, _targetFrequency, Time.deltaTime * 8f);
        }
    }

    // ==========================================
    // 단발성 사운드 (서버 -> 모든 클라이언트 방송)
    // ==========================================

    [ClientRpc]
    public void PlayAttackSoundClientRpc() { if (owner.monsterData.attackClip != null) voiceSource.PlayOneShot(owner.monsterData.attackClip); }

    [ClientRpc]
    public void PlayHitSoundClientRpc() { if (owner.monsterData.hitClip != null) voiceSource.PlayOneShot(owner.monsterData.hitClip); }

    [ClientRpc]
    public void PlayDeathSoundClientRpc() { if (owner.monsterData.deathClip != null) voiceSource.PlayOneShot(owner.monsterData.deathClip); }

    [ClientRpc]
    public void PlayScreamSoundClientRpc() { if (owner.monsterData.screamClip != null) voiceSource.PlayOneShot(owner.monsterData.screamClip); }

    [ClientRpc]
    public void PlayTargetScreamClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (owner.monsterData.screamClip != null) AudioSource.PlayClipAtPoint(owner.monsterData.screamClip, Camera.main.transform.position, 1f);
    }

    [ClientRpc]
    public void PlayFootstepClientRpc()
    {
        AudioClip[] clips = owner.monsterData.footstepClips;
        if (clips != null && clips.Length > 0) footstepSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }
}