using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class BreakableEffect : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float gravityScale = 1f;

    private SpriteRenderer sr;
    private AudioSource audioSource;
    private Rigidbody2D rb;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        
        // Safely get or add AudioSource
        if (!TryGetComponent<AudioSource>(out audioSource))
        {
            // Only add if we actually have a sound to play
            if (breakSound != null) 
                audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Safely get or add Rigidbody2D
        if (!TryGetComponent<Rigidbody2D>(out rb))
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        
        // Apply physics only if RB exists
        if (rb != null)
        {
            rb.gravityScale = gravityScale;
            rb.angularVelocity = Random.Range(-300f, 300f);
            rb.linearVelocity = Random.insideUnitCircle * 2f;
        }
    }

    void Start()
    {
        if (audioSource != null && breakSound != null)
        {
            audioSource.PlayOneShot(breakSound);
        }

        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        float waitTime = Mathf.Max(0, lifetime - fadeDuration);
        yield return new WaitForSeconds(waitTime);

        float timer = 0f;
        // Capture start color safely
        Color startColor = (sr != null) ? sr.color : Color.white;

        while (timer < fadeDuration)
        {
            if (sr != null)
            {
                float alpha = Mathf.Lerp(startColor.a, 0f, timer / fadeDuration);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}