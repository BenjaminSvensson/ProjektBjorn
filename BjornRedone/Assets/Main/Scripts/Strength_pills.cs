using UnityEngine;

public class Strength_pills : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] AudioSource pickupSound;
    public float addstrength = 0.25f;

    [Header("Magnet Settings")]
    [SerializeField] private float magnetRadius = 5f; // Distance to trigger magnet
    [SerializeField] private float magnetSpeed = 10f; // Flight speed

    private Transform playerTransform;

    void Start()
    {
        // Automatically find the player by Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    void Update()
    {
        // If player exists, check distance
        if (playerTransform != null)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // If inside range, fly towards player
            if (distance < magnetRadius)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    playerTransform.position,
                    magnetSpeed * Time.deltaTime
                );
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // 1. Play Sound (Uses PlayClipAtPoint so Destroy() doesn't cut the audio off)
            if (pickupSound != null && pickupSound.clip != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound.clip, transform.position);
            }

            // 2. Apply Strength Effect
            Multipliers playerMultipliers = collision.GetComponent<Multipliers>();
            if (playerMultipliers != null)
            {
                playerMultipliers.strength += addstrength; // Increase player's strength
            }

            // 3. Remove Pill
            Destroy(gameObject);
        }
    }

    // Visualize range in Editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red; // Changed to red to distinguish from speed pill
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}