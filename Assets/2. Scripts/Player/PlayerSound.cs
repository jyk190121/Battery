using UnityEngine;
using Unity.Netcode;

public class PlayerSound : NetworkBehaviour
{
    [Header("참조")]
    public PlayerMove playerMove;
    public PlayerController playerController;
    public PlayerStateManager stateManager;

    [Header("오디오 소스")]
    public AudioSource footstepSource;
    public AudioSource voiceSource;
    public AudioSource breathSource;

    [Header("사운드 클립")]
    public AudioClip[] concreteSteps;
    public AudioClip[] grassSteps;
    public AudioClip[] metalSteps;
    public AudioClip jumpClip;
    public AudioClip attackClip;
    public AudioClip hurtClip;

    [Header("숨소리 클립 (로컬 전용)")]
    public AudioClip normalBreathClip;
    public AudioClip heavyBreathClip;

    [Header("설정")]
    public float walkStepInterval = 0.45f;
    public float runStepInterval = 0.3f;
    private float _stepTimer;

    public override void OnNetworkSpawn()
    {
        // [수정됨] 숨소리는 오직 '내 캐릭터(IsOwner)'일 때만 재생을 시작합니다!
        if (IsOwner && breathSource != null && normalBreathClip != null)
        {
            breathSource.clip = normalBreathClip;
            breathSource.loop = true;
            breathSource.Play();
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        // 본인(Owner)의 상태만 체크해서 사운드를 냅니다.
        HandleBreathingLocal();
        HandleFootsteps();
    }

    // ==========================================
    // 1. 숨소리 (로컬 전용 - 남에게는 절대 안 들림)
    // ==========================================
    private void HandleBreathingLocal()
    {
        if (stateManager == null || breathSource == null) return;

        bool isHeavy = stateManager.IsExhausted || (stateManager.CurrentStamina < 10f);
        AudioClip target = isHeavy ? heavyBreathClip : normalBreathClip;

        if (breathSource.clip != target)
        {
            breathSource.clip = target;
            breathSource.Play();
        }
    }

    // ==========================================
    // 2. 발자국 동기화 (네트워크 3D)
    // ==========================================
    private void HandleFootsteps()
    {
        if (!playerMove.IsGrounded || !playerMove.IsMoving || playerMove.IsCrouching) return;

        bool isRunning = playerMove.currentSpeed > playerMove.walkSpeed + 0.1f;
        float currentInterval = isRunning ? runStepInterval : walkStepInterval;

        _stepTimer += Time.deltaTime;

        if (_stepTimer >= currentInterval)
        {
            // 1. 내 컴퓨터에서 즉시 재생 (딜레이 방지)
            PlayFootstepLocal(playerMove.currentGroundTag);

            // 2. 서버를 통해 다른 사람들에게 방송
            PlayFootstepServerRpc(playerMove.currentGroundTag);

            // 3. 몬스터 어그로 신고 (스텔스 기능)
            float noiseLevel = isRunning ? 1.5f : 0.8f;
            playerController.ReportNoiseServerRpc(transform.position, noiseLevel, playerController.isInsideFacility.Value);

            _stepTimer = 0f;
        }
    }

    [ServerRpc]
    private void PlayFootstepServerRpc(string groundTag)
    {
        PlayFootstepClientRpc(groundTag);
    }

    [ClientRpc]
    private void PlayFootstepClientRpc(string groundTag)
    {
        if (IsOwner) return; // 나는 위에서 틀었으니 중복 재생 무시
        PlayFootstepLocal(groundTag);
    }

    private void PlayFootstepLocal(string tag)
    {
        AudioClip[] clips = concreteSteps;
        if (tag == "Grass") clips = grassSteps;
        else if (tag == "Metal") clips = metalSteps;

        if (clips.Length > 0)
            footstepSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }

    // ==========================================
    // 3. 액션 동기화 (Jump, Attack, Hurt)
    // ==========================================
    public void RequestJumpSound()
    {
        if (!IsOwner) return;
        PlayActionSoundServerRpc("Jump");
    }

    public void RequestAttackSound()
    {
        if (!IsOwner) return;
        PlayActionSoundServerRpc("Attack");
    }

    [ServerRpc]
    private void PlayActionSoundServerRpc(string actionType)
    {
        PlayActionSoundClientRpc(actionType);
    }

    [ClientRpc]
    private void PlayActionSoundClientRpc(string actionType)
    {
        AudioClip clip = null;
        if (actionType == "Jump") clip = jumpClip;
        else if (actionType == "Attack") clip = attackClip;

        if (clip != null && voiceSource != null) voiceSource.PlayOneShot(clip);
    }

    // 피격음은 기존대로 서버에서 직접 쏩니다
    [ClientRpc]
    public void PlayHurtSoundClientRpc()
    {
        if (hurtClip != null && voiceSource != null) voiceSource.PlayOneShot(hurtClip);
    }
}