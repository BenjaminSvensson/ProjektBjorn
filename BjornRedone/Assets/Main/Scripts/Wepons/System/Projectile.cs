using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    private float damage;
    private float speed;
    private Vector2 direction;
    private float lifetime = 5f;
    private bool isEnemyProjectile = false; // --- NEW: Track ownership ---

    public void Initialize(Vector2 dir, float spd, float dmg, bool isEnemy = false)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        isEnemyProjectile = isEnemy; // --- NEW ---
        
        // Rotate to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Set velocity immediately
        GetComponent<Rigidbody2D>().linearVelocity = direction * speed;
        
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore other Projectiles to prevent mid-air collisions
        if (other.GetComponent<Projectile>()) return;

        // --- ENEMY BULLET LOGIC ---
        if (isEnemyProjectile)
        {
            // Hit Player
            if (other.CompareTag("Player"))
            {
                PlayerLimbController player = other.GetComponent<PlayerLimbController>();
                if (player != null)
                {
                    player.TakeDamage(damage, direction);
                }
                Destroy(gameObject);
                return;
            }

            // Ignore Enemies (Friendly fire off)
            if (other.GetComponent<EnemyLimbController>()) return;
        }
        // --- PLAYER BULLET LOGIC ---
        else
        {
            // Hit Enemy
            EnemyLimbController enemy = other.GetComponent<EnemyLimbController>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, direction);
                Destroy(gameObject);
                return;
            }

            // Hit LootContainer
            LootContainer loot = other.GetComponent<LootContainer>();
            if (loot != null)
            {
                loot.TakeDamage(damage, direction);
                Destroy(gameObject);
                return;
            }

            // Ignore Player (Self damage off)
            if (other.CompareTag("Player")) return;
        }

        // --- WALLS / OBSTACLES ---
        // Destroy on any solid object that wasn't handled above (Ground, Walls)
        if (!other.isTrigger)
        {
            // Optional: Spawn spark effect here
            Destroy(gameObject);
        }
    }
}