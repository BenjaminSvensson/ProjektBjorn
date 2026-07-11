using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
[RequireComponent(typeof(AudioSource))]
public class TextSequenceEffect : MonoBehaviour
{
    [Header("Sequence Timings")]
    [SerializeField] private float delayBeforeStart = 0f;
    [SerializeField] private float fadeInDuration = 1.0f;
    [SerializeField] private float stayDuration = 2.0f;
    [SerializeField] private float fadeOutDuration = 1.0f;

    [Header("Audio")]
    [SerializeField] private AudioClip sequenceSound;
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;

    private TextMeshProUGUI textComponent;
    private AudioSource audioSource;

    void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        audioSource = GetComponent<AudioSource>();

        // Initialize with zero alpha so it doesn't flicker on frame 1
        Color c = textComponent.color;
        c.a = 0;
        textComponent.color = c;
    }

    void Start()
    {
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // Initial Delay
        if (delayBeforeStart > 0)
            yield return new WaitForSecondsRealtime(delayBeforeStart);

        // Play Sound
        if (sequenceSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(sequenceSound, volume);
        }

        // Fade In
        yield return StartCoroutine(FadeText(0f, 1f, fadeInDuration));

        // Stay
        yield return new WaitForSecondsRealtime(stayDuration);

        // Fade Out
        yield return StartCoroutine(FadeText(1f, 0f, fadeOutDuration));

        // Optional: Deactivate object when done
        gameObject.SetActive(false);
    }

    private IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        Color startColor = textComponent.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled so it works even if game is paused
            float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            textComponent.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        // Ensure final value is exact
        textComponent.color = new Color(startColor.r, startColor.g, startColor.b, endAlpha);
    }
}