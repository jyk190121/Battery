using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 몬스터의 모든 사운드(RPC, 애니메이션 이벤트 등)를 전담하는 스크립트
/// </summary>
public class MonsterSound : NetworkBehaviour
{
    [Header("Audio Sources")]
    public AudioSource voiceSource; // 괴성, 숨소리용
    public AudioSource footstepSource; // 발자국 전용

    // 1. [애니메이션 이벤트용] 걷기 애니메이션에서 호출됨 (서버 통신 X, 로컬 재생)
    public void PlayFootstep()
    {
        // 3D 사운드 재생
        //footstepSource.PlayOneShot(SoundManager.Instance.GetSfxClip(SfxSound.MONS_FOOTSTEP));
    }

    // 2. [특수 이벤트용] 타겟팅된 비명 소리 
    [ClientRpc]
    public void PlayTargetScreamClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfx(SfxSound.MONS_SCREAM);
        }
    }

    // 3. [광역 이벤트용] 모두에게 들리는 포효
    [ClientRpc]
    public void PlayGlobalRoarClientRpc()
    {
        //voiceSource.PlayOneShot(SoundManager.Instance.GetSfxClip(SfxSound.MONS_ROAR));
    }

    /// <summary>
    /// [광역 사운드] 서버가 모든 클라이언트에게 몬스터의 소리를 재생하라고 명령합니다.
    /// </summary>
    /// <param name="soundType">재생할 사운드의 종류 (Enum)</param>
    [ClientRpc]
    public void PlayGlobalSfxClientRpc(SfxSound soundType)
    {
        if (voiceSource != null && SoundManager.Instance != null)
        {
            AudioClip clip = SoundManager.Instance.GetSfxClip(soundType);
            if (clip != null)
            {
                // 몬스터의 위치에서 3D 사운드로 재생됩니다.
                voiceSource.PlayOneShot(clip);
            }
        }
    }
}