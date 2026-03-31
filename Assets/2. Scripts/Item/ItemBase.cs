using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    [Header("Item Settings")]
    public ItemDataSO itemData; // 아까 만든 설계도(SO) 연결
    public bool isEquipped = false;

    protected Rigidbody rb;
    protected Collider col;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    // 아이템 삭제/풀링 요청
    public virtual void RequestDespawn()
    {
        // 우선은 단순 파괴/비활성화로 구현
        gameObject.SetActive(false);
    }

    // 소유권 변경 (줍기/버리기 핵심 로직)
    public virtual void RequestChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;

        if (isPickingUp)
        {
            // 1. 손에 붙이기
            transform.SetParent(targetHand);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            // 2. 물리 끄기
            if (rb != null) rb.isKinematic = true;
            if (col != null) col.enabled = false;
        }
        else
        {
            // 1. 부모 해제
            transform.SetParent(null);

            // 2. 물리 켜기
            if (rb != null) rb.isKinematic = false;
            if (col != null) col.enabled = true;
        }
    }
}