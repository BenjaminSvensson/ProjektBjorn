using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class SplashScreenManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The name of the scene to load after the splash screen finishes.")]
    public string nextSceneName;

    [Tooltip("How long the logo stays visible in the center (seconds).")]
    public float displayDuration = 2.5f;

    [Tooltip("How long it takes to fade in and out.")]
    public float fadeDuration = 1.0f;

    [Header("Optional Animation")]
    [Tooltip("If you have a specific animation clip (like a spinning logo), drag the Animator here.")]
    public Animator optionalAnimator;
    [Tooltip("The name of the trigger parameter in your Animator controller (e.g., 'Play').")]
    public string animationTriggerName = "Play";

    private CanvasGroup canvasGroup;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        
        // Ensure we start invisible so the player doesn't miss anything
        canvasGroup.alpha = 0f;

        // Start the sequence
        StartCoroutine(PlaySplashSequence());
    }

    IEnumerator PlaySplashSequence()
    {
        // 1. FADE IN (Make it visible)
        yield return StartCoroutine(Fade(0f, 1f));

        // 2. PLAY ANIMATION / WAIT
        // Now that it is fully visible, we trigger the animation or start the timer
        if (optionalAnimator != null && !string.IsNullOrEmpty(animationTriggerName))
        {
            optionalAnimator.SetTrigger(animationTriggerName);
        }

        // Wait for the display duration so the player can actually absorb the logo
        yield return new WaitForSeconds(displayDuration);

        // 3. FADE OUT
        yield return StartCoroutine(Fade(1f, 0f));

        // 4. LOAD SCENE
        LoadNextScene();
    }

    IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            // Mathf.Lerp creates a smooth transition between values
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = endAlpha;
    }

    void LoadNextScene()
    {
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError($"Cannot load scene '{nextSceneName}'. Make sure it is added to the Build Settings!");
        }
    }
}