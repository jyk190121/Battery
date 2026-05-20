using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : NetworkBehaviour
{
    private PlayerAnim _playerAnim;
    private PlayerMove _playerMove;
    private PlayerEquipment _playerEquipment;
    private PlayerSound _playerSound;

    [Header("상태")]
    public bool isAttacking = false;

    [Header("무기관련 스텟변화")]
    public float attackDamage = 0f;
    public float attackRange = 3.0f;

    public override void OnNetworkSpawn()
    {
        _playerAnim = GetComponent<PlayerAnim>();
        _playerMove = GetComponent<PlayerMove>();
        _playerEquipment = GetComponent<PlayerEquipment>();
        _playerSound = GetComponent<PlayerSound>();
    }

    void Update()
    {
        if (!IsOwner) return;

        // 마우스 좌클릭 입력
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (CanAttack())
            {
                RequestAttackServerRpc();
            }
        }
    }

    private bool CanAttack()
    {
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null && !controller.CanUseItem) return false;

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

        // 1. 현재 손에 든 무기의 HandType(한손/양손) 알아내기
        HandType currentHandType = HandType.OneHand; // 맨손일 때 기본값
        PlayerInventory inventory = GetComponent<PlayerInventory>();

        if (inventory != null && inventory.HeldItem != null)
        {
            currentHandType = inventory.HeldItem.itemData.handType;
        }

        // 2. 애니메이션 실행 (어떤 손 무기인지 정보를 같이 넘김)
        if (_playerAnim != null) _playerAnim.PlayAttack(currentHandType);

        // 이동 제한
        if (_playerMove != null) _playerMove.SetControlLock(true);

        if (_playerSound != null) _playerSound.RequestAttackSound();

        StopCoroutine(nameof(AttackTimeoutRoutine));
        StartCoroutine(nameof(AttackTimeoutRoutine));
    }

    IEnumerator AttackTimeoutRoutine()
    {
        yield return new WaitForSeconds(1.5f); // 공격 애니메이션 평균 시간보다 조금 길게 설정
        if (isAttacking)
        {
            OnAttackEnd();
        }
    }

    // 애니메이션 이벤트(OnAttackEnd) 연동 필수
    public void OnAttackEnd()
    {
        isAttacking = false;
        if (_playerMove != null) _playerMove.SetControlLock(false);
    }

    void PerformHitDetection()
    {
        float currentDamage = attackDamage;

        PlayerInventory inventory = GetComponent<PlayerInventory>();
        ItemBase currentItem = (inventory != null) ? inventory.HeldItem : null;

        // 💡 핵심: 손에 든 아이템이 존재하고, 그게 '무기(Item_Weapon)' 클래스라면?
        if (currentItem != null && currentItem is Item_Weapon equippedWeapon)
        {
            // 무기 SO 데이터에 적혀있는 수치로 데미지와 사거리를 덮어씌웁니다!
            attackDamage = equippedWeapon.attackPower;
        }

        PlayerController localPlayer = GetComponent<PlayerController>();

        if (localPlayer != null && localPlayer.isSnared.Value)
        {
            MonsterController attachedMonster = GetComponentInChildren<MonsterController>();

            if (attachedMonster != null)
            {
                attachedMonster.TakeDamage(attackDamage);

                // 💡 [추가] 타격 성공 시 무기 내구도 차감 (서버 권한 실행)
                if (IsServer && currentItem is Item_Weapon weapon)
                {
                    weapon.DeductDurability(inventory);
                }

                return;
            }
        }

        Vector3 attackCenter = transform.position + (Vector3.up * 1.0f) + (transform.forward * (attackRange * 0.5f));

        // 타격 구체의 크기(반지름): 사거리의 절반 + 추가 보정치(0.5f)
        // 이렇게 하면 내 몸 바로 앞부터 목표 사거리까지, 그리고 좌우로도 아주 널널하게 판정이 생깁니다.
        float attackRadius = (attackRange * 0.5f) + 0.5f;

        // 해당 구체 공간(거대한 비눗방울 모양) 안에 들어온 모든 콜라이더를 싹 다 가져옵니다.
        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, attackRadius);

        foreach (Collider col in hitColliders)
        {
            // [방어 코드] 내 캐릭터 자신이나 내 무기 콜라이더는 무시
            if (col.gameObject == this.gameObject || col.transform.root == this.transform.root) continue;

            // 구체 안에 몬스터가 있다면 타격! (동료 얼굴에 붙어있어도 구체 안에만 있으면 무조건 맞습니다)
            if (col.TryGetComponent<MonsterController>(out var monster))
            {
                monster.TakeDamage(currentDamage);
                Debug.Log($"[Server] 몬스터 {monster.name} 타격 성공! (범위: {attackRange}m)");

                if (IsServer && currentItem is Item_Weapon weapon)
                {
                    weapon.DeductDurability(inventory);
                }

                // 한 번 휘둘러서 한 마리만 때리고 싶다면 여기서 break; 
                // 다수의 몬스터를 동시에 때리고(스플래시) 싶다면 break를 지우시면 됩니다.
                break;
            }
        }

        // [테스트용] 유니티 씬(Scene) 뷰에서 무기를 휘두를 때마다 투명한 빨간색 구체가 그려져 타격 범위를 눈으로 확인할 수 있습니다.
        // 빌드된 게임에서는 보이지 않습니다.
        StartCoroutine(DrawDebugSphere(attackCenter, attackRadius, 1.0f));
    }

    // 씬 뷰에서 공격 범위를 시각적으로 보여주기 위한 헬퍼 코루틴 (선택사항)
    private IEnumerator DrawDebugSphere(Vector3 center, float radius, float duration)
    {
        float time = 0;
        while (time < duration)
        {
            // 십자선 모양으로 대략적인 구체의 위치와 크기를 표시
            Debug.DrawRay(center, Vector3.up * radius, Color.red);
            Debug.DrawRay(center, Vector3.down * radius, Color.red);
            Debug.DrawRay(center, Vector3.left * radius, Color.red);
            Debug.DrawRay(center, Vector3.right * radius, Color.red);
            Debug.DrawRay(center, Vector3.forward * radius, Color.red);
            Debug.DrawRay(center, Vector3.back * radius, Color.red);

            time += Time.deltaTime;
            yield return null;
        }
    }

    public void AttemptAttack()
    {
        // CanAttack() 내부에서 이미 IsGrounded와 HasWeapon을 체크함
        if (CanAttack())
        {
            RequestAttackServerRpc();
        }
    }

    // 무기 장착 시 호출될 메서드
    public void SetAttackDamage(float damage)
    {
        attackDamage = damage;
    }

    public void ResetAttackDamage()
    {
        attackDamage = 0f;
    }
}