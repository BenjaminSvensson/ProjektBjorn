using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))] 
[RequireComponent(typeof(AudioSource))] // --- NEW: Require AudioSource ---
public class BushShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeMagnitude = 0.1f;
    
    [Header("Noise Settings")]
    [SerializeField] private float noiseRadius = 8f;
    [SerializeField] private LayerMask enemyLayer;

    // --- NEW: Audio Settings ---
    [Header("Audio Settings")]
    [SerializeField] private AudioClip rustleSound;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.2f;
    // --- END NEW ---

    private Transform visualTransform;
    private bool isShaking = false;
    private AudioSource audioSource; // --- NEW: Reference ---

    // --- FIX: Use Awake instead of Start ---
    // Awake runs immediately upon creation, BEFORE collision events.
    void Awake()
    {
        visualTransform = transform;
        audioSource = GetComponent<AudioSource>(); // --- NEW: Get Component ---
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isShaking && (other.CompareTag("Player") || other.GetComponent<EnemyAI>() != null))
        {
            PlayRustleSound(); // --- NEW: Play Sound ---
            StartCoroutine(ShakeRoutine());
        }

        if (other.CompareTag("Player"))
        {
            AlertEnemies();
        }
    }

    // --- NEW: Audio Helper ---
    private void PlayRustleSound()
    {
        if (audioSource != null && rustleSound != null)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(rustleSound);
        }
    }
    // --- END NEW ---

    private void AlertEnemies()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, noiseRadius, enemyLayer);
        
        foreach (var hit in hits)
        {
            EnemyAI ai = hit.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.OnHearNoise(transform.position);
            }
        }
    }

    private IEnumerator ShakeRoutine()
    {
        isShaking = true;
        
        // --- FIX: Capture position right now ---
        // This ensures we shake around the correct spot, even if the generator 
        // moved the bush after Awake but before this Trigger.
        Vector3 startPos = visualTransform.localPosition;
        
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            
            // Apply offset relative to the startPos we captured
            visualTransform.localPosition = startPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        visualTransform.localPosition = startPos;
        isShaking = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, noiseRadius);
    }
}