using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : NetworkBehaviour
{
    private PlayerAnim _playerAnim;
    private PlayerMove _playerMove;
    private PlayerEquipment _playerEquipment;

    [Header("상태")]
    public bool isAttacking = false;

    public override void OnNetworkSpawn()
    {
        _playerAnim = GetComponent<PlayerAnim>();
        _playerMove = GetComponent<PlayerMove>();
        _playerEquipment = GetComponent<PlayerEquipment>();
    }

    void Update()
    {
        if (!IsOwner) return;

        // 마우스 좌클릭 입력
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (CanAttack())
            {
                RequestAttackServerRpc();
            }
        }
    }

    private bool CanAttack()
    {
        // 1. 공격 중이 아니고
        // 2. 땅에 붙어 있으며
        // 3. PlayerEquipment를 통해 무기를 들고 있는지 확인
        return !isAttacking &&
               (_playerMove != null && _playerMove.IsGrounded) &&
               (_playerEquipment != null && _playerEquipment.HasWeapon);
    }

    [ServerRpc]
    private void RequestAttackServerRpc()
    {
        // [보안/로직 검증] 서버에서도 이 플레이어가 정말 공격 가능한 상태인지 체크
        if (isAttacking) return;

        ExecuteAttackClientRpc();

        // 2. [추가] 서버에서 실제 타격 판정 수행
        PerformHitDetection();
    }

    [ClientRpc]
    private void ExecuteAttackClientRpc()
    {
        StartAttackEffect();
    }

    private void StartAttackEffect()
    {
        isAttacking = true;

        // 애니메이션 실행
        if (_playerAnim != null) _playerAnim.PlayAttack();

        // 이동 제한
        if (_playerMove != null) _playerMove.SetControlLock(true);
    }

    // 애니메이션 이벤트(OnAttackEnd) 연동 필수
    public void OnAttackEnd()
    {
        isAttacking = false;
        if (_playerMove != null) _playerMove.SetControlLock(false);
    }

    void PerformHitDetection()
    {
        // 공격 범위 설정 (무기 아이템 데이터에서 가져오는 것이 좋음)
        float attackRange = 2.0f;
        float attackDamage = 20f; // 예시 데미지

        // 레이캐스트 또는 OverlapSphere로 몬스터 탐색
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, attackRange))
        {
            if (hit.collider.TryGetComponent<MonsterController>(out var monster))
            {
                // 서버 권한으로 몬스터에게 데미지 부여
                monster.TakeDamage(attackDamage);
                Debug.Log($"[Server] 몬스터 {monster.name} 타격 성공!");
            }
        }
    }
}