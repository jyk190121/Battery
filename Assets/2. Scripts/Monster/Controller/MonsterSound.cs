using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 몬스터의 모든 사운드를 전담하며, MonsterData(SO)에 등록된 클립을 사용합니다.
/// </summary>
public class MonsterSound : NetworkBehaviour
{
    [Header("참조")]
    public MonsterController owner; // MonsterData를 들고 있는 주인

    [Header("Audio Sources")]
    public AudioSource voiceSource;    // 비명, 공격, 피격 (입소리)
    public AudioSource footstepSource; // 발자국
    public AudioSource loopSource;  // 숨소리 (Loop 설정 권장)

    public override void OnNetworkSpawn()
    {
        if (owner == null) owner = GetComponent<MonsterController>();

        // 시작과 동시에 데이터에 있는 숨소리를 무한 재생 (루프)
        if (loopSource != null && owner.monsterData.breathClip != null)
        {
            loopSource.clip = owner.monsterData.breathClip;
            loopSource.loop = true;
            loopSource.Play();
        }
    }

    // ==========================================
    // 1. 발자국 (애니메이션 이벤트 또는 타이머에서 호출)
    // ==========================================
    public void PlayFootstep()
    {
        AudioClip[] clips = owner.monsterData.footstepClips;
        if (clips != null && clips.Length > 0)
        {
            // 여러 발소리 중 하나를 랜덤으로 재생 (자연스러움)
            footstepSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }
    }

    // ==========================================
    // 2. 특수 이벤트 (타겟팅 비명 - 인형 전용 등)
    // ==========================================
    [ClientRpc]
    public void PlayTargetScreamClientRpc(ClientRpcParams clientRpcParams = default)
    {
        // SoundManager의 UI 재생 기능을 빌리되, 소리 파일은 내 데이터에서 꺼냅니다.
        // 만약 SoundManager.Instance.PlaySfx(AudioClip clip) 함수가 있다면 그것을 사용하세요.
        // 여기서는 직접 2D 사운드(점프스케어)로 재생한다고 가정합니다.
        if (owner.monsterData.screamClip != null)
        {
            // 타겟 플레이어의 화면 전체에 2D로 울려 퍼지게 함
            AudioSource.PlayClipAtPoint(owner.monsterData.screamClip, Camera.main.transform.position, 1f);
        }
    }

    // ==========================================
    // 3. 광역 사운드 (공격, 피격, 포효)
    // ==========================================

    // 공격 시 호출 (모든 유저에게 3D로 들림)
    [ClientRpc]
    public void PlayAttackSoundClientRpc()
    {
        if (owner.monsterData.attackClip != null)
        {
            voiceSource.PlayOneShot(owner.monsterData.attackClip);
        }
    }

    // 피격 시 호출 (모든 유저에게 3D로 들림)
    [ClientRpc]
    public void PlayHitSoundClientRpc()
    {
        if (owner.monsterData.hitClip != null)
        {
            voiceSource.PlayOneShot(owner.monsterData.hitClip);
        }
    }
}