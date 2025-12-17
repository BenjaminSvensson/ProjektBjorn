using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("The enemy will only shoot if the player is within this distance.")]
    [SerializeField] private float shootRange = 10f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float projectileKnockback = 3f; // --- NEW ---
    
    [Header("Spawn Configuration")]
    [Tooltip("The points from which projectiles will be fired.")]
    [SerializeField] private Transform[] spawnPoints;
    
    [Tooltip("If TRUE: Shoots in the direction of the Spawn Point's Green Arrow (Up).\nIf FALSE: Shoots outward from the Enemy's center.")]
    [SerializeField] private bool useRotationForDirection = false;

    [Header("Timing")]
    [SerializeField] private bool autoFire = true;
    [SerializeField] private float fireRate = 2f;
    [SerializeField] private float initialDelay = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioSource audioSource;

    private float timer;
    private Transform player;

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        timer = initialDelay; 

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (!autoFire || player == null) return;

        // 1. Check Distance
        float distSq = (player.position - transform.position).sqrMagnitude;
        if (distSq > shootRange * shootRange) return; 

        // 3. Fire Timer
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Shoot();
            timer = fireRate;
        }
    }

    public void Shoot()
    {
        if (projectilePrefab == null) return;

        if (audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // If no points assigned, just shoot one forward
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            SpawnProjectile(transform.position, transform.right);
            return;
        }

        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;

            Vector2 direction;

            if (useRotationForDirection)
            {
                direction = point.up; 
            }
            else
            {
                direction = (point.position - transform.position).normalized;
                if (direction == Vector2.zero) direction = transform.right; 
            }

            SpawnProjectile(point.position, direction);
        }
    }

    private void SpawnProjectile(Vector2 position, Vector2 direction)
    {
        GameObject obj = Instantiate(projectilePrefab, position, Quaternion.identity);
        Projectile proj = obj.GetComponent<Projectile>();
        if (proj != null)
        {
            // Pass knockback and true (isEnemy)
            proj.Initialize(direction, projectileSpeed, damage, projectileKnockback, true);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, shootRange);

        if (spawnPoints == null) return;
        Gizmos.color = Color.red;
        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;
            Vector2 dir = useRotationForDirection ? (Vector2)point.up : ((Vector2)point.position - (Vector2)transform.position).normalized;
            if (dir == Vector2.zero) dir = transform.right;

            Gizmos.DrawLine(point.position, (Vector2)point.position + dir * 1f);
            Gizmos.DrawWireSphere(point.position, 0.1f);
        }
    }
}