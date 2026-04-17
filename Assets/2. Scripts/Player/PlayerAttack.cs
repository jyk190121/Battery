using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : NetworkBehaviour
{
    private PlayerAnim _playerAnim;
    private PlayerMove _playerMove;
    private PlayerEquipment _playerEquipment; // 이제 Equipment를 참조합니다.

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
        ExecuteAttackClientRpc();
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
}