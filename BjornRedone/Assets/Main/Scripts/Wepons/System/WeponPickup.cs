using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    [Header("Physics")]
    [SerializeField] private float throwForce = 5f;
    [SerializeField] private float pickupCooldown = 1.0f; // Prevent picking up immediately after dropping

    private Rigidbody2D rb;
    private Collider2D col;
    private bool canBePickedUp = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    public void InitializeDrop(Vector2 direction)
    {
        canBePickedUp = false;
        
        // Enable physics
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = 5f;
            rb.angularDamping = 5f;
            rb.AddForce(direction * throwForce, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-10f, 10f), ForceMode2D.Impulse);
        }

        if (col != null) col.isTrigger = false;

        StartCoroutine(PickupCooldownRoutine());
    }

    private IEnumerator PickupCooldownRoutine()
    {
        yield return new WaitForSeconds(pickupCooldown);
        canBePickedUp = true;
        
        // Optional: Become a trigger to make pickup smoother (no kicking it around)
        // if (col != null) col.isTrigger = true; 
    }

    public WeaponData GetWeaponData()
    {
        return weaponData;
    }

    public bool CanPickup()
    {
        return canBePickedUp;
    }
}