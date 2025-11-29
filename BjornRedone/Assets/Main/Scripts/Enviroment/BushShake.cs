using UnityEngine;
using System.Collections; // We need this for Coroutines

/// <summary>
/// Attach this to any bush (or similar object) that should shake and
/// play a sound when the player walks through it.
/// 
/// REQUIRES:
/// 1. A Collider2D set to "Is Trigger = true".
/// 2. An AudioSource component.
/// 3. (Recommended) A SortingGroup and DynamicYSorter.
/// 4. The Player GameObject must have the tag "Player".
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class BushShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [Tooltip("How long the shake effect should last.")]
    [SerializeField] private float shakeDuration = 0.5f;
    [Tooltip("How fast the bush should wiggle back and forth.")]
    [SerializeField] private float shakeSpeed = 50f;
    [Tooltip("How far (in degrees) the bush should wiggle.")]
    [SerializeField] private float shakeAngle = 5f;

    [Header("Audio")]
    [Tooltip("The 'rustle' sound to play when triggered.")]
    [SerializeField] private AudioClip shakeSound;
    private AudioSource audioSource;

    [Header("Trigger Settings")]
    [Tooltip("The tag of the object that triggers the shake (e.g., 'Player').")]
    [SerializeField] private string triggerTag = "Player";
    
    // --- Private State ---
    private bool isShaking = false;
    private Quaternion originalRotation;

    void Awake()
    {
        // Get the AudioSource component on this GameObject
        audioSource = GetComponent<AudioSource>();
        // Store our starting rotation so we can return to it
        originalRotation = transform.localRotation;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object that entered is the player AND we're not already shaking
        if (other.CompareTag(triggerTag) && !isShaking)
        {
            // Start the shake
            StartCoroutine(ShakeCoroutine());
        }
    }

    private IEnumerator ShakeCoroutine()
    {
        isShaking = true;
        float elapsed = 0f;

        // Play the sound (if one is assigned)
        if (shakeSound != null)
        {
            audioSource.PlayOneShot(shakeSound);
        }

        // Loop for the duration of the shake
        while (elapsed < shakeDuration)
        {
            // We use a Sine wave to create a smooth, rapid back-and-forth wiggle.
            // Time.time * shakeSpeed makes it fast.
            // shakeAngle determines how far it wiggles.
            float zAngle = Mathf.Sin(Time.time * shakeSpeed) * shakeAngle;
            
            // Apply this wiggle to our original rotation
            transform.localRotation = originalRotation * Quaternion.Euler(0, 0, zAngle);
            
            elapsed += Time.deltaTime;
            yield return null; // Wait for the next frame
        }
        
        // Reset to the original rotation when done
        transform.localRotation = originalRotation;
        isShaking = false;
    }
}