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
    public AudioSource voiceSource;    // 비명, 공격, 피격, 사망 (단발성)
    public AudioSource footstepSource; // 발자국
    public AudioSource ambientSource;  // 숨소리, 추격음 (루프성)

    // ==========================================
    // [추가됨] 마스터의 아이디어 반영: 인스펙터에서 몹마다 다르게 설정 가능!
    // ==========================================
    [Header("발소리 간격 설정")]
    public float walkStepInterval = 0.45f; // 평소 순찰할 때 발소리 간격
    public float runStepInterval = 0.3f;   // 추격할 때(빠를 때) 발소리 간격

    private float _stepTimer;

    public override void OnNetworkSpawn()
    {
        if (owner == null) owner = GetComponent<MonsterController>();
    }

    void Update()
    {
        if (owner == null || owner.monsterData == null || ambientSource == null) return;

        MonsterStateType currentState = owner.CurrentStateNet.Value;

        // 1. 사망 상태면 모든 루프 사운드를 끔
        if (currentState == MonsterStateType.Dead)
        {
            if (ambientSource.isPlaying) ambientSource.Stop();
            return;
        }

        // 2. 상태에 따라 재생할 루프 클립 결정 (기본은 평소 숨소리)
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

        // 3. 클립이 바뀌었으면 오디오 소스 교체 후 재생 (각 클라이언트가 알아서 처리)
        if (ambientSource.clip != targetLoopClip && targetLoopClip != null)
        {
            ambientSource.clip = targetLoopClip;
            ambientSource.loop = true;
            if (!ambientSource.isPlaying) ambientSource.Play();
        }

        // ==========================================
        // 4. 발소리 타이머 로직 (애니메이션 이벤트 대체)
        // ==========================================
        if (owner.IsServer)
        {
            if (owner.navAgent.enabled && owner.navAgent.velocity.sqrMagnitude > 0.1f)
            {
                float currentInterval = (currentState == MonsterStateType.Chase) ? runStepInterval : walkStepInterval;

                _stepTimer += Time.deltaTime;

                if (_stepTimer >= currentInterval)
                {
                    // ServerRpc를 거치지 않고, 서버가 직접 ClientRpc를 호출하여 모두에게 방송!
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

    // ==========================================
    // 단발성 사운드 (서버 -> 모든 클라이언트 방송)
    // ==========================================

    [ClientRpc]
    public void PlayAttackSoundClientRpc()
    {
        if (owner.monsterData.attackClip != null) voiceSource.PlayOneShot(owner.monsterData.attackClip);
    }

    [ClientRpc]
    public void PlayHitSoundClientRpc()
    {
        if (owner.monsterData.hitClip != null) voiceSource.PlayOneShot(owner.monsterData.hitClip);
    }

    [ClientRpc]
    public void PlayDeathSoundClientRpc()
    {
        if (owner.monsterData.deathClip != null) voiceSource.PlayOneShot(owner.monsterData.deathClip);
    }

    [ClientRpc]
    public void PlayScreamSoundClientRpc()
    {
        if (owner.monsterData.screamClip != null) voiceSource.PlayOneShot(owner.monsterData.screamClip);
    }

    [ClientRpc]
    public void PlayTargetScreamClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (owner.monsterData.screamClip != null)
        {
            AudioSource.PlayClipAtPoint(owner.monsterData.screamClip, Camera.main.transform.position, 1f);
        }
    }

    // ==========================================
    // 타이머 이벤트 전용 (서버가 계산하고 ClientRpc로 모두에게 3D 사운드 방송)
    // ==========================================
    [ClientRpc]
    public void PlayFootstepClientRpc()
    {
        AudioClip[] clips = owner.monsterData.footstepClips;
        if (clips != null && clips.Length > 0)
        {
            footstepSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }
    }
}