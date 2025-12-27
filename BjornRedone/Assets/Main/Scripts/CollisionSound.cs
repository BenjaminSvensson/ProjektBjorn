using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CollisionSound : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("Randomly picks one of these sounds on impact.")]
    [SerializeField] private AudioClip[] impactSounds;

    [Header("Settings")]
    [Tooltip("Minimum speed required to trigger a sound. Prevents noise when sliding slowly.")]
    [SerializeField] private float velocityThreshold = 1.0f;
    
    [Tooltip("The speed at which the sound plays at maximum volume.")]
    [SerializeField] private float maxVolumeVelocity = 10f;

    [Tooltip("Base volume multiplier.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseVolume = 1.0f;

    [Tooltip("Randomize pitch slightly for variety.")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.2f;

    [Tooltip("Minimum time between sounds to prevent spamming.")]
    [SerializeField] private float cooldown = 0.1f;

    private AudioSource audioSource;
    private float lastSoundTime = -10f;

    void Awake()
    {
        // Try to get existing AudioSource, or add a lightweight one if missing
        if (!TryGetComponent<AudioSource>(out audioSource))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.maxDistance = 20f; // Fade out distance
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 1. Check Cooldown
        if (Time.time - lastSoundTime < cooldown) return;

        // 2. Check Velocity
        // relativeVelocity is the speed of the impact
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed >= velocityThreshold)
        {
            PlayImpactSound(impactSpeed);
        }
    }

    private void PlayImpactSound(float impactSpeed)
    {
        if (impactSounds == null || impactSounds.Length == 0) return;

        // Pick random clip
        AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];
        
        if (clip != null)
        {
            // Calculate Volume based on speed
            // Example: If threshold is 2 and max is 10. 
            // A hit of 6 gives t = 0.5. Volume is 50%.
            float t = Mathf.InverseLerp(velocityThreshold, maxVolumeVelocity, impactSpeed);
            float volume = Mathf.Lerp(0.1f, 1f, t) * baseVolume;

            // Pitch Randomization
            if (randomizePitch)
            {
                audioSource.pitch = Random.Range(minPitch, maxPitch);
            }
            else
            {
                audioSource.pitch = 1f;
            }

            // Play
            audioSource.PlayOneShot(clip, volume);
            lastSoundTime = Time.time;
        }
    }
}