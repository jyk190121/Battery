using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class PlayerController : NetworkBehaviour
{
    [Header("Base Data")]
    [SerializeField] private Player _playerData; // SO 데이터
    public Player Data => _playerData; // 읽기 전용 프로퍼티
    public bool IsDead { get; private set; }

    // 컴포넌트들을 미리 캐싱하여 다른 곳에서 쉽게 찾게 할 수도 있습니다.
    public PlayerStateManager StateManager { get; private set; }
    public PlayerInteraction Interaction { get; private set; }

    // 서버와 몬스터가 즉시 참조할 수 있는 전역(Static) 출석부
    public static List<PlayerController> AllPlayers = new List<PlayerController>();

    private void Awake()
    {
        StateManager = GetComponent<PlayerStateManager>();
        Interaction = GetComponent<PlayerInteraction>();
    }

    public override void OnNetworkSpawn()
    {
        if (!AllPlayers.Contains(this))
        {
            AllPlayers.Add(this);
            Debug.Log($"[서버 알림] 플레이어 접속: 현재 인원 {AllPlayers.Count}명");
        }

        if (IsServer)
        {
            // 게임 시작 시 SO의 원본 값을 복사해 현재 HP로 설정
            StateManager.currentHealth.Value = _playerData.maxHealth;
        }
    }

    // 플레이어가 튕기거나 방을 나갈 때 출석부에서 제거합니다.
    public override void OnNetworkDespawn()
    {
        if (AllPlayers.Contains(this))
        {
            AllPlayers.Remove(this);
            Debug.Log($"[서버 알림] 플레이어 퇴장: 남은 인원 {AllPlayers.Count}명");
        }

        base.OnNetworkDespawn();
    }

    // [ServerRpc] 외부(몬스터 등)에서 데미지를 줄 때 호출
    [ServerRpc]
    public void TakeDamageServerRpc(float damage)
    {
        if (IsDead) return;

        StateManager.currentHealth.Value -= damage;

        if (StateManager.currentHealth.Value <= 0)
        {
            StateManager.currentHealth.Value = 0;
            Die();
        }
    }

    private void Die()
    {
        IsDead = true;
        Debug.Log($"{gameObject.name}가 사망했습니다.");
        // 사망 애니메이션 실행, 콜라이더 끄기, 리스폰 로직 등 처리
    }
}