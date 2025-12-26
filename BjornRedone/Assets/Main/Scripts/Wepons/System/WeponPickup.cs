using UnityEngine;
using System.Collections;

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
    [SerializeField] private float groundFriction = 15f;
    [SerializeField] private float rotationSpeed = 200f;

    [Header("Throw Damage")]
    [SerializeField] private float baseThrowDamage = 2f;
    [SerializeField] private float knockbackForce = 8f;

    private Rigidbody2D rb;
    private Collider2D myCollider;
    private bool isFlying = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = groundFriction; 
    }

    void Start()
    {
        if (!isFlying && currentAmmoCount < 0 && weaponData != null)
        {
            currentAmmoCount = weaponData.magazineSize;
        }
    }

    void FixedUpdate()
    {
        if (isFlying && rb.linearVelocity.sqrMagnitude < 1f)
        {
            isFlying = false;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isFlying) return;

        EnemyLimbController enemy = collision.gameObject.GetComponent<EnemyLimbController>();
        if (enemy != null)
        {
            float totalDamage = baseThrowDamage;
            if (weaponData != null) totalDamage += weaponData.meleeDamageBonus;

            Vector2 hitDir = rb.linearVelocity.normalized;
            enemy.TakeDamage(totalDamage, hitDir);

            if (collision.gameObject.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyRb))
            {
                enemyRb.AddForce(hitDir * knockbackForce, ForceMode2D.Impulse);
            }

            HandleImpact();
            return;
        }

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
        isFlying = false;
        rb.linearVelocity = -rb.linearVelocity * 0.3f; 
        rb.angularVelocity *= 0.5f;
    }

    // --- NEW: Called by DamageSource (Trap) ---
    public void GetStuck()
    {
        isFlying = false;
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        if (myCollider) myCollider.enabled = false;
        this.enabled = false; // Stop Update loop
    }

    public void InitializeDrop(Vector2 direction, float force = 5f, int ammo = -1)
    {
        if (weaponData != null)
        {
            currentAmmoCount = (ammo >= 0) ? ammo : weaponData.magazineSize;
        }

        if (rb != null)
        {
            rb.linearDamping = 0.5f; 
            rb.AddForce(direction * force, ForceMode2D.Impulse);
            rb.angularVelocity = Random.Range(-rotationSpeed, rotationSpeed);
            isFlying = true; 
            
            Invoke(nameof(RestoreFriction), 0.5f);
        }
    }

    public void IgnorePhysicsCollisionWith(Collider2D[] otherColliders, float duration)
    {
        StartCoroutine(IgnoreCollisionRoutine(otherColliders, duration));
    }

    private IEnumerator IgnoreCollisionRoutine(Collider2D[] others, float duration)
    {
        if (myCollider == null || others == null) yield break;
        foreach (var col in others) if (col != null && col != myCollider) Physics2D.IgnoreCollision(col, myCollider, true);
        yield return new WaitForSeconds(duration);
        foreach (var col in others) if (col != null && col != myCollider) Physics2D.IgnoreCollision(col, myCollider, false);
    }

    private void RestoreFriction()
    {
        if (rb != null) rb.linearDamping = groundFriction;
    }

    public bool CanPickup() { return !isFlying; }
    public WeaponData GetWeaponData() { return weaponData; }
}