using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // Needed to load scenes
using System.Collections;

public class BossDeathManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("The EXACT name of your Credits scene file")]
    public string creditsSceneName = "Credits";

    [Header("UI Connections")]
    [Tooltip("Drag the black Panel (with CanvasGroup) here")]
    public CanvasGroup fadeOverlay; 

    [Header("Timing")]
    public float waitBeforeFade = 3.0f; // Time for boss explosion/scream
    public float fadeDuration = 2.0f;   // How long the screen takes to turn black

    [Header("Audio (Optional)")]
    public AudioClip victorySound;
    private AudioSource audioSource;
    private bool isDead = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Safety check: Ensure the screen starts clear
        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }
    }

    // Call this function when Health <= 0
    public void TriggerBossDeath()
    {
        if (isDead) return;
        isDead = true;

        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        // 1. Stop the Boss Logic
        // Disable the AI script so he stops attacking while dying
        var ai = GetComponent<PenguinEnemyAI>();
        if (ai != null) ai.enabled = false;

        // Disable movement/physics
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false; // Cannot be hit anymore

        // 2. Play Death Animation / Sound
        var anim = GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Die"); // Assuming you have a Die trigger

        if (victorySound && audioSource)
        {
            audioSource.PlayOneShot(victorySound);
        }

        // 3. Wait for the drama (explosions, animation finishing)
        yield return new WaitForSeconds(waitBeforeFade);

        // 4. Fade to Black
        if (fadeOverlay != null)
        {
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadeOverlay.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null;
            }
            fadeOverlay.alpha = 1f;
        }

        // 5. Load the Credits
        SceneManager.LoadScene(creditsSceneName);
    }
}