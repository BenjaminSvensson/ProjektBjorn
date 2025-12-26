using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Data")]
    public WeaponData weaponData;

    [Header("State")]
    [Tooltip("Current rounds in the mag. -1 means 'Uninitialized' (will default to full on start).")]
    public int currentAmmoCount = -1; 

    [Header("Physics")]
    [SerializeField] private float groundFriction = 5f;
    [SerializeField] private float rotationSpeed = 200f;

    [Header("Throw Damage")]
    [SerializeField] private float baseThrowDamage = 15f;
    [SerializeField] private float knockbackForce = 8f;

    private Rigidbody2D rb;
    private bool isFlying = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = groundFriction; 
    }

    void Start()
    {
        // If placed in the scene manually (not dropped), ensure it has full ammo
        if (!isFlying && currentAmmoCount < 0 && weaponData != null)
        {
            currentAmmoCount = weaponData.magazineSize;
        }
    }

    void FixedUpdate()
    {
        // Stop "flying" state when slow enough, allowing pickup
        if (isFlying && rb.linearVelocity.sqrMagnitude < 1f)
        {
            isFlying = false;
        }
    }

    // --- NEW: Handle collision damage while flying ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isFlying) return;

        // 1. Check for Enemy
        EnemyLimbController enemy = collision.gameObject.GetComponent<EnemyLimbController>();
        if (enemy != null)
        {
            float totalDamage = baseThrowDamage;
            if (weaponData != null) totalDamage += weaponData.meleeDamageBonus;

            // Use current velocity direction for impact
            Vector2 hitDir = rb.linearVelocity.normalized;
            
            enemy.TakeDamage(totalDamage, hitDir);

            // Apply physics knockback to enemy if they have a rigidbody
            if (collision.gameObject.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyRb))
            {
                enemyRb.AddForce(hitDir * knockbackForce, ForceMode2D.Impulse);
            }

            // Weapon reacts to impact
            HandleImpact();
            return;
        }

        // 2. Check for Loot Container (e.g., Crates)
        LootContainer container = collision.gameObject.GetComponent<LootContainer>();
        if (container != null)
        {
            float totalDamage = baseThrowDamage;
            if (weaponData != null) totalDamage += weaponData.meleeDamageBonus;

            container.TakeDamage(totalDamage, rb.linearVelocity.normalized);
            
            HandleImpact();
            return;
        }
    }

    private void HandleImpact()
    {
        // Stop flying immediately so we don't double-hit or hurt the player on rebound
        isFlying = false;
        
        // Dampen velocity to simulate a heavy hit
        rb.linearVelocity = -rb.linearVelocity * 0.3f; 
        rb.angularVelocity *= 0.5f;
    }

    /// <summary>
    /// Called when the player drops this weapon.
    /// </summary>
    public void InitializeDrop(Vector2 direction, float force = 5f, int ammo = -1)
    {
        if (weaponData != null)
        {
            currentAmmoCount = (ammo >= 0) ? ammo : weaponData.magazineSize;
        }

        if (rb != null)
        {
            // Reset damping momentarily so it flies
            rb.linearDamping = 0.5f; 
            rb.AddForce(direction * force, ForceMode2D.Impulse);
            rb.angularVelocity = Random.Range(-rotationSpeed, rotationSpeed);
            isFlying = true; 
            
            // Restore high damping after a short time to stop sliding
            Invoke(nameof(RestoreFriction), 0.5f);
        }
    }

    private void RestoreFriction()
    {
        if (rb != null) rb.linearDamping = groundFriction;
    }

    public bool CanPickup()
    {
        return !isFlying;
    }

    public WeaponData GetWeaponData()
    {
        return weaponData;
    }
}