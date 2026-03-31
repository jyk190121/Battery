using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    [Header("Item Settings")]
    public ItemDataSO itemData; // 아이템 설계도 (이름, 타입, 가격 등)
    public bool isEquipped = false;

    // 물리 연산 및 상태 제어용 변수
    protected Rigidbody rb;
    protected Collider col;
    protected bool isThrown = false; // 현재 던져져서 날아가는 중인지 여부

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    // 아이템 삭제/풀링 요청 (정산 완료 후나 소모품 사용 후 호출됨)
    public virtual void RequestDespawn()
    {
        gameObject.SetActive(false);
    }

    // 소유권 변경 (줍기 / 손에서 놓기) 핵심 로직
    public virtual void RequestChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;

        if (isPickingUp)
        {
            // 1. 물리 연산 먼저 초기화 (반드시 isKinematic = true 보다 먼저 해야 에러가 안 남!)
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (col != null) col.enabled = false;

            // 2. 손에 붙이기
            if (targetHand != null)
            {
                transform.SetParent(targetHand);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                // 손 위치가 인스펙터에 안 들어가 있으면 콘솔에 빨간 에러를 띄웁니다.
                Debug.LogError("🚨 줍기 실패: PlayerInventory에 Hand Transform이 할당되지 않았습니다!");
            }
        }
        else
        {
            // 버리기 
            transform.SetParent(null);

            if (rb != null) rb.isKinematic = false;
            if (col != null) col.enabled = true;
        }
    }

    // ==========================================================
    // [투척 및 착지 물리 제어 로직]
    // ==========================================================

    // PlayerInventory에서 G키를 눌러 던졌을 때 호출됨
    public virtual void BeginThrownState()
    {
        isThrown = true;
    }

    // 유니티 물리 충돌 콜백 (물체끼리 부딪혔을 때 자동 실행)
    protected virtual void OnCollisionEnter(Collision collision)
    {
        // 1. 던져진 상태이고
        // 2. 부딪힌 대상의 레이어가 "Ground" (바닥)일 때만 작동
        if (isThrown && collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            StopMovementGracefully();
        }
    }

    // 바닥에 닿는 순간 미끄러지거나 통통 튀는 것을 막고 자연스럽게 쓰러지게 함
    protected virtual void StopMovementGracefully()
    {
        isThrown = false; // 착지 완료

        if (rb != null)
        {
            // 1. 닿는 순간의 모든 관성과 속도를 강제로 0으로 만듦 (바닥 뚫림, 미끄러짐 완벽 차단)
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // 2. 연산 모드를 기본으로 돌려서, 자체 무게중심에 따라 바닥에 "툭" 하고 쓰러지게 둠
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }
}