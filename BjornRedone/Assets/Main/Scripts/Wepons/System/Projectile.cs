using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    private float damage;
    private float speed;
    private Vector2 direction;
    private float lifetime = 5f;

    public void Initialize(Vector2 dir, float spd, float dmg)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        
        // Rotate to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Set velocity immediately
        GetComponent<Rigidbody2D>().linearVelocity = direction * speed;
        
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore Player and other Projectiles
        if (other.CompareTag("Player") || other.GetComponent<Projectile>()) return;

        // Hit Enemy
        EnemyLimbController enemy = other.GetComponent<EnemyLimbController>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, direction);
            Destroy(gameObject);
            return;
        }

        // Hit Walls/Obstacles (Assuming they are not triggers)
        if (!other.isTrigger)
        {
            // Optional: Spawn spark effect
            Destroy(gameObject);
        }
    }
}