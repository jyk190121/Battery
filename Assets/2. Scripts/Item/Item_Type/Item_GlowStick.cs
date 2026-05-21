using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Item_GlowStick : ItemBase
{
    [Header("Glow Stick Settings")]
    [Tooltip("형광스틱 불빛을 쏠 Light 컴포넌트")]
    public Light glowLight;

    [Tooltip("스틱 모델의 Renderer (선택사항: 스틱 자체를 발광시키기 위함)")]
    public Renderer glowMeshRenderer;

    [Tooltip("빛이 유지되는 시간 (초 단위)")]
    public float lifeTime = 60f;

    [Tooltip("던지는 힘 (플래시뱅과 동일하게 설정)")]
    public float throwForce = 15f;

    [ColorUsage(true, true)] // 인스펙터에서 HDR 컬러를 선택할 수 있게 함
    public Color activeEmissionColor = Color.green;

    protected override void Awake()
    {
        base.Awake();
        // 맵에 처음 스폰되었을(부러뜨리기 전) 때는 불을 꺼둡니다.
        if (glowLight != null) glowLight.enabled = false;
    }

    // ==========================================
    // 1. 아이템 사용 (좌클릭 시 호출됨) -> 플래시뱅과 100% 동일한 구조
    // ==========================================
    public override void ExecuteUseItem(Vector3 direction)
    {
        // [몸에 걸림 방지] 물리 엔진이 켜지기 전에 플레이어 몸 밖(앞쪽)으로 살짝 빼줍니다.
        transform.position += direction * 0.8f;

        // 1. 장착 상태 해제 및 부모 자식 관계 끊기
        ExecuteChangeOwnership(false, null);

        if (itemPhysicsRigidbody != null)
        {
            itemPhysicsRigidbody.isKinematic = false;
            itemPhysicsRigidbody.linearVelocity = Vector3.zero; // 이전 속도 초기화
            itemPhysicsRigidbody.angularVelocity = Vector3.zero;
            itemPhysicsRigidbody.WakeUp();
        }

        if (itemPhysicalCollider != null)
        {
            itemPhysicalCollider.isTrigger = false;
        }

        // 2. 물리 상태를 "던져진 상태"로 전환 (Ground 충돌 감지용)
        BeginThrownState();

        // 3. 레이어 변경 (EquippedItem -> Item)
        gameObject.layer = LayerMask.NameToLayer("Default");

        // 4. [비주얼 활성화] 형광스틱 빛 켜기 (모든 클라이언트)
        TurnOnGlowEffect();

        // 5. 힘 가하기 (서버에서만 물리적 충격 적용)
        if (IsServer)
        {
            StopAllCoroutines();
            itemPhysicsRigidbody.AddForce(direction * throwForce, ForceMode.Impulse);
            StartCoroutine(GlowAndDestroyRoutine());
        }
    }

    private void TurnOnGlowEffect()
    {
        if (glowLight != null) glowLight.enabled = true;

        if (glowMeshRenderer != null)
        {
            glowMeshRenderer.material.EnableKeyword("_EMISSION");
            glowMeshRenderer.material.SetColor("_EmissionColor", activeEmissionColor);
        }
    }

    // ==========================================
    // 2. 타이머 및 소멸 처리 (서버 전용)
    // ==========================================
    private IEnumerator GlowAndDestroyRoutine()
    {
        // 1. 설정한 시간(예: 60초) 동안 바닥을 비추며 대기
        yield return new WaitForSeconds(lifeTime);

        // 2. 모든 클라이언트에게 빛을 끄라고 지시
        TurnOffGlowClientRpc();

        // 3. 불이 꺼지고 1초 뒤에 네트워크상에서 오브젝트를 영구 삭제
        yield return new WaitForSeconds(1.0f);

        if (NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(); // 서버가 오브젝트를 완벽히 파괴 및 청소
        }
    }

    [ClientRpc]
    private void TurnOffGlowClientRpc()
    {
        if (glowLight != null) glowLight.enabled = false;

        if (glowMeshRenderer != null)
        {
            glowMeshRenderer.material.SetColor("_EmissionColor", Color.clear);
        }
    }
}