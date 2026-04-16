using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class VentController : NetworkBehaviour
{
    [Header("Effects")]
    [Tooltip("환풍구 자체에서 소리를 내기 위한 3D AudioSource")]
    public AudioSource localAudioSource;
    [Tooltip("환풍구에서 떨어지는 먼지 파티클")]
    public ParticleSystem dustParticle;

    [Header("Settings")]
    [Tooltip("경고음이 울린 후 실제 몬스터가 등장하기까지의 지연 시간(초)")]
    public float spawnDelay = 3.0f;

    // 현재 이 환풍구가 몬스터를 뱉어내고 있는 중인지 확인하는 플래그 (중복 스폰 방지용)
    public bool IsSpawning { get; private set; } = false;

    private void Awake()
    {
        if (localAudioSource == null) localAudioSource = GetComponent<AudioSource>();

        // 3D 사운드 설정 
        localAudioSource.spatialBlend = 1.0f; // 1.0 = 완전한 3D 사운드
        localAudioSource.maxDistance = 100f;   // 소리가 들리는 최대 거리
    }

    public void TriggerSpawn(MonsterData data)
    {
        if (!IsServer || IsSpawning) return;
        StartCoroutine(SpawnRoutine(data));
    }

    private IEnumerator SpawnRoutine(MonsterData data)
    {
        IsSpawning = true;

        PlayVentEffectClientRpc(); // 클라이언트들에게 연출 실행 지시

        yield return new WaitForSeconds(spawnDelay);

        if (data.monsterPrefab != null)
        {
            NetworkObject netObj = MonsterPool.Instance.GetMonster(data.monsterPrefab, transform.position, transform.rotation);

            if (netObj != null)
            {
                EnemyManager.Instance.RegisterActiveMonster(netObj);
            }
        }
        IsSpawning = false;
    }

    [ClientRpc]
    private void PlayVentEffectClientRpc()
    {
        AudioClip creakClip = SoundManager.Instance.GetSfxClip(SfxSound.VENT_CREAK);

        if (creakClip != null && localAudioSource != null)
        {
            localAudioSource.pitch = Random.Range(0.9f, 1.1f);
            localAudioSource.PlayOneShot(creakClip);
        }

        if (dustParticle != null)
        {
            dustParticle.Play();
        }
    }
}