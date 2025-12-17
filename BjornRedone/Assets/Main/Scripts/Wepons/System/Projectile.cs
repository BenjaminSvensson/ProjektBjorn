using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float lifetime = 5f;
    
    [Header("Ownership")]
    [Tooltip("Check this box if this is an Enemy Bullet (Hurts Player).\nUncheck it for Player Bullets (Hurts Enemies).")]
    [SerializeField] private bool isEnemyProjectile = false;

    private float damage;
    private float speed;
    private float knockbackForce; // --- NEW ---
    private Vector2 direction;

    // Updated Initialize to accept knockback
    public void Initialize(Vector2 dir, float spd, float dmg, float kb, bool isEnemy)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        knockbackForce = kb; // --- NEW ---
        isEnemyProjectile = isEnemy;
        
        // Rotate to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Set velocity immediately
        GetComponent<Rigidbody2D>().linearVelocity = direction * speed;
        
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore other Projectiles
        if (other.GetComponent<Projectile>()) return;

        bool hitSomething = false;

        // --- ENEMY BULLET ---
        if (isEnemyProjectile)
        {
            if (other.CompareTag("Player"))
            {
                PlayerLimbController player = other.GetComponent<PlayerLimbController>();
                if (player != null) player.TakeDamage(damage, direction);
                hitSomething = true;
            }
            else if (other.GetComponent<EnemyLimbController>()) return; // Ignore friendly fire
        }
        // --- PLAYER BULLET ---
        else
        {
            // Hit Enemy
            EnemyLimbController enemy = other.GetComponent<EnemyLimbController>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, direction);
                // Note: Enemy script handles its own knockback inside TakeDamage usually, 
                // but we can apply extra physics force below if needed.
                hitSomething = true;
            }

            // Hit LootContainer
            LootContainer loot = other.GetComponent<LootContainer>();
            if (loot != null)
            {
                loot.TakeDamage(damage, direction);
                hitSomething = true;
            }

            if (other.CompareTag("Player")) return; // Ignore self
        }

        // --- GENERIC PHYSICS KNOCKBACK (Props, Limbs, Weapons) ---
        // If we hit something that has a Rigidbody2D (and it's not the shooter)
        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            // Don't knockback the shooter
            if (isEnemyProjectile && other.GetComponent<EnemyLimbController>()) { }
            else if (!isEnemyProjectile && other.CompareTag("Player")) { }
            else
            {
                // Apply instant force
                rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
                hitSomething = true;
            }
        }

        // --- WALLS / OBSTACLES ---
        if (!other.isTrigger || hitSomething)
        {
            Destroy(gameObject);
        }
    }
}