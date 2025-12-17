using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Data")]
    public WeaponData weaponData;

    [Header("Physics")]
    [SerializeField] private float groundFriction = 5f;
    [SerializeField] private float rotationSpeed = 200f;

    private Rigidbody2D rb;
    private bool isFlying = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = groundFriction; 
    }

    void FixedUpdate()
    {
        // Stop "flying" state when slow enough, allowing pickup
        // This prevents instantly picking up the weapon you just threw
        if (isFlying && rb.linearVelocity.sqrMagnitude < 1f)
        {
            isFlying = false;
        }
    }

    public void InitializeDrop(Vector2 direction, float force = 5f)
    {
        if (rb != null)
        {
            rb.AddForce(direction * force, ForceMode2D.Impulse);
            rb.angularVelocity = Random.Range(-rotationSpeed, rotationSpeed);
            isFlying = true; // Mark as flying so we can't pick it up instantly
        }
    }

    public bool CanPickup()
    {
        return !isFlying;
    }

    public WeaponData GetWeaponData()
    {
        return weaponData;
    }

    // REMOVED OnTriggerEnter2D to prevent double-pickup logic (duplication bug).
    // The PlayerCollision script handles the pickup interaction centrally.
}