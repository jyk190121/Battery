using System.Collections;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 섬광탄 아이템의 투척, 폭발 타이머, 반경 내 타겟(플레이어 시야 차단 및 몬스터 스턴) 판정을 처리.
/// </summary>
public class Item_Flashbang : ItemBase
{
    [Header("Flashbang Settings")]
    public float throwForce = 15f;
    public float explosionDelay = 1.0f;
    public float flashRadius = 10f;
    public float stunDuration = 4.0f;

    [Header("Layer Mask Settings")]
    public LayerMask targetMask;
    public LayerMask obstacleMask;
    public LayerMask playerLayer;
    public LayerMask monsterLayer;

    public override void ExecuteUseItem(Vector3 direction)
    {
        ExecuteChangeOwnership(false, null);

        if (itemPhysicsRigidbody != null)
        {
            itemPhysicsRigidbody.isKinematic = false;
            itemPhysicsRigidbody.linearVelocity = Vector3.zero;
            itemPhysicsRigidbody.angularVelocity = Vector3.zero;
            itemPhysicsRigidbody.WakeUp();
        }

        if (itemPhysicalCollider != null)
        {
            itemPhysicalCollider.isTrigger = false;
        }

        BeginThrownState();

        if (IsServer)
        {
            itemPhysicsRigidbody.AddForce(direction * throwForce, ForceMode.Impulse);
            StartCoroutine(ExplosionRoutine());
        }
    }

    private IEnumerator ExplosionRoutine()
    {
        yield return new WaitForSeconds(explosionDelay);
        ExplodeServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    private void ExplodeServerRpc()
    {
        Vector3 explosionOrigin = transform.position;
        Collider[] hitTargets = Physics.OverlapSphere(explosionOrigin, flashRadius, targetMask);

        foreach (Collider targetCollider in hitTargets)
        {
            Vector3 targetCenter = targetCollider.bounds.center;
            Vector3 directionToTarget = (targetCenter - explosionOrigin).normalized;
            float distanceToTarget = Vector3.Distance(explosionOrigin, targetCenter);

            if (!Physics.Raycast(explosionOrigin, directionToTarget, distanceToTarget, obstacleMask))
            {
                ApplyEffect(targetCollider.gameObject);
            }
        }

        if (IsServer)
        {
            NetworkObject.Despawn();
        }
    }

    private void ApplyEffect(GameObject targetObject)
    {
        int objectLayerMask = 1 << targetObject.layer;

        if (targetObject.TryGetComponent(out NetworkObject networkObj))
        {
            if (networkObj.IsOwner && targetObject.TryGetComponent(out FlashEffect flashEffect))
            {
                flashEffect.TriggerFlash(3.0f);
            }
        }
        else if ((objectLayerMask & monsterLayer) != 0)
        {
            if (IsServer)
            {
                if (targetObject.TryGetComponent(out MonsterController monster))
                {
                    monster.ApplyStun(stunDuration);
                }
                else if (targetObject.GetComponentInParent<MonsterController>() != null)
                {
                    targetObject.GetComponentInParent<MonsterController>().ApplyStun(stunDuration);
                }
            }
        }
    }
}