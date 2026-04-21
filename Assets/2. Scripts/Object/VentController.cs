using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class VentController : NetworkBehaviour
{
    [Header("Visuals")]
    [Tooltip("닫혀 있는 상태의 환풍구 오브젝트")]
    public GameObject closedVentObject;
    [Tooltip("열려 있는 상태의 환풍구 오브젝트")]
    public GameObject openVentObject;

    [Header("Effects")]
    public AudioSource localAudioSource;
    public ParticleSystem dustParticle;

    [Header("Settings")]
    public float spawnDelay = 3.0f;

    // 환풍구 상태를 네트워크를 통해 동기화 
    private NetworkVariable<bool> isVentOpen = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsSpawning { get; private set; } = false;

    private void Awake()
    {
        if (localAudioSource == null) localAudioSource = GetComponent<AudioSource>();

        localAudioSource.spatialBlend = 1.0f;
        localAudioSource.maxDistance = 50f;
    }

    public override void OnNetworkSpawn()
    {
        // 상태가 변할 때 실행될 함수 연결
        isVentOpen.OnValueChanged += OnVentStateChanged;

        // 초기 상태 설정
        RefreshVentVisuals(isVentOpen.Value);
    }

    public override void OnNetworkDespawn()
    {
        isVentOpen.OnValueChanged -= OnVentStateChanged;
    }

    // 상태 값이 변하면 모든 클라이언트에서 실행됨
    private void OnVentStateChanged(bool previousValue, bool newValue)
    {
        RefreshVentVisuals(newValue);
    }

    // 실제 오브젝트를 끄고 켜는 함수
    private void RefreshVentVisuals(bool isOpen)
    {
        if (closedVentObject != null) closedVentObject.SetActive(!isOpen);
        if (openVentObject != null) openVentObject.SetActive(isOpen);
    }

    public void TriggerSpawn(MonsterData data)
    {
        if (!IsServer || IsSpawning) return;
        StartCoroutine(SpawnRoutine(data));
    }

    private IEnumerator SpawnRoutine(MonsterData data)
    {
        IsSpawning = true;

        // 1. 서버에서 상태를 변경 
        isVentOpen.Value = true;

        // 2. 소리 및 파티클 연출 실행
        PlayVentEffectClientRpc();

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