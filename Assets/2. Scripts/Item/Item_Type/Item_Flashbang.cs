using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class Item_Flashbang : ItemBase
{
    [Header("Flashbang Settings")]
    public float throwForce = 15f;
    public float explosionDelay = 1.0f;
    public float flashRadius = 10f;

    [Header("Layer Mask Settings")]
    public LayerMask targetMask;
    public LayerMask obstacleMask;
    public LayerMask playerLayer;
    public LayerMask monsterLayer;

    public override void ExecuteUseItem(Vector3 direction)
    {
        // 1. [중요] 장착 상태 해제 및 부모 자식 관계 끊기
        // ExecuteChangeOwnership(false, null)이 내부적으로 TryRemoveParent와 isKinematic 해제, NetTransform 활성화를 처리함
        ExecuteChangeOwnership(false, null);

        if (itemPhysicalCollider != null)
        {
            itemPhysicalCollider.isTrigger = false;
        }


        // 2. 물리 상태를 "던져진 상태"로 전환 (Ground 충돌 감지용)
        BeginThrownState();

        // 3. 레이어 변경 (EquippedItem -> Item)
        gameObject.layer = LayerMask.NameToLayer("Item");

        if (itemPhysicsRigidbody != null)
        {
            // 4. 힘 가하기 (서버에서만 물리적 충격 적용 권장)
            if (IsServer)
            {
                itemPhysicsRigidbody.AddForce(direction * throwForce, ForceMode.Impulse);
                StartCoroutine(ExplosionRoutine());
            }
        }
    }

    private IEnumerator ExplosionRoutine()
    {
        yield return new WaitForSeconds(explosionDelay);
        PerformExplosionRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void PerformExplosionRpc()
    {
        Vector3 explosionOrigin = transform.position + Vector3.up * 0.1f;
        Collider[] targets = Physics.OverlapSphere(explosionOrigin, flashRadius, targetMask);

        foreach (var target in targets)
        {
            Vector3 targetCenter = target.bounds.center;
            Vector3 dir = (targetCenter - explosionOrigin).normalized;
            float dist = Vector3.Distance(explosionOrigin, targetCenter);

            if (!Physics.Raycast(explosionOrigin, dir, dist, obstacleMask))
            {
                ApplyEffect(target.gameObject);
            }
        }

        if (IsServer) NetworkObject.Despawn();
    }

    private void ApplyEffect(GameObject targetObj)
    {
        int objLayerMask = 1 << targetObj.layer;
        if ((objLayerMask & playerLayer) != 0)
        {
            if (targetObj.GetComponent<NetworkObject>().IsOwner) Debug.Log("Local Player Blinded");
        }
        else if ((objLayerMask & monsterLayer) != 0)
        {
            Debug.Log("Monster Stunned");
        }
    }
}