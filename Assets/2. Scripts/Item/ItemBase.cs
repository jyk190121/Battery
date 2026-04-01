using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    public ItemDataSO itemData;
    public bool isEquipped = false;
    protected Rigidbody rb;
    protected Collider col;
    protected bool isThrown = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public virtual void RequestDespawn() { gameObject.SetActive(false); }

    public virtual void RequestChangeOwnership(bool isPickingUp, Transform targetHand)
    {
        isEquipped = isPickingUp;
        isThrown = false;
        if (isPickingUp)
        {
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
            if (col != null) col.enabled = false;
            transform.SetParent(targetHand);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.SetParent(null);
            if (rb != null) rb.isKinematic = false;
            if (col != null) col.enabled = true;
        }
    }

    public virtual void BeginThrownState() { isThrown = true; }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (isThrown && collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            isThrown = false;
        }
    }
}