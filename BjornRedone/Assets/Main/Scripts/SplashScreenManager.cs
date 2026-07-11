using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem;
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
    private RectTransform skipFill;
    private TMP_Text skipLabel;
    private float skipProgress;
    private bool isLoading;
    private bool sceneLoadIssued;
    private const float SkipHoldDuration = 0.65f;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        BuildSkipPrompt();
        
        // Ensure we start invisible so the player doesn't miss anything
        canvasGroup.alpha = 0f;

        // Start the sequence
        StartCoroutine(PlaySplashSequence());
    }

    void Update()
    {
        if (isLoading || Keyboard.current == null) return;

        bool holding = Keyboard.current.spaceKey.isPressed;
        float direction = holding ? 1f / SkipHoldDuration : -2.5f;
        skipProgress = Mathf.Clamp01(skipProgress + direction * Time.unscaledDeltaTime);
        if (skipFill != null)
        {
            Vector2 max = skipFill.anchorMax;
            max.x = skipProgress;
            skipFill.anchorMax = max;
        }
        if (skipLabel != null)
            skipLabel.text = holding ? $"SKIPPING  {Mathf.RoundToInt(skipProgress * 100f)}%" : "HOLD SPACE  //  SKIP";

        if (skipProgress >= 1f) BeginSkip();
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

    private void BeginSkip()
    {
        if (isLoading) return;
        StopAllCoroutines();
        StartCoroutine(SkipRoutine());
    }

    private IEnumerator SkipRoutine()
    {
        isLoading = true;
        float from = canvasGroup != null ? canvasGroup.alpha : 1f;
        float elapsed = 0f;
        const float duration = 0.18f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(from, 0f, elapsed / duration);
            yield return null;
        }
        LoadNextScene();
    }

    private void BuildSkipPrompt()
    {
        GameObject root = new GameObject("Cutscene Skip", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject pill = new GameObject("Skip Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        pill.transform.SetParent(root.transform, false);
        RectTransform pillRect = pill.GetComponent<RectTransform>();
        pillRect.anchorMin = pillRect.anchorMax = new Vector2(1f, 0f);
        pillRect.pivot = new Vector2(1f, 0f);
        pillRect.anchoredPosition = new Vector2(-32f, 32f);
        pillRect.sizeDelta = new Vector2(250f, 58f);
        Image pillImage = pill.GetComponent<Image>();
        pillImage.color = new Color(0.02f, 0.02f, 0.025f, 0.88f);
        BjornUIStyle.ApplyRounded(pillImage);
        pill.GetComponent<Button>().onClick.AddListener(BeginSkip);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(pill.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 10f);
        labelRect.offsetMax = new Vector2(-18f, -14f);
        skipLabel = labelObject.GetComponent<TextMeshProUGUI>();
        skipLabel.text = "HOLD SPACE  //  SKIP";
        skipLabel.fontSize = 15f;
        skipLabel.fontStyle = FontStyles.Bold;
        skipLabel.alignment = TextAlignmentOptions.Center;
        skipLabel.color = Color.white;
        skipLabel.raycastTarget = false;

        GameObject track = new GameObject("Progress Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        track.transform.SetParent(pill.transform, false);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.anchoredPosition = new Vector2(0f, 8f);
        trackRect.sizeDelta = new Vector2(-32f, 3f);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.18f);
        BjornUIStyle.ApplyRounded(trackImage);

        GameObject fill = new GameObject("Progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        skipFill = fill.GetComponent<RectTransform>();
        skipFill.anchorMin = Vector2.zero;
        skipFill.anchorMax = new Vector2(0f, 1f);
        skipFill.offsetMin = Vector2.zero;
        skipFill.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = Color.white;
        BjornUIStyle.ApplyRounded(fillImage);
    }

    void LoadNextScene()
    {
        if (sceneLoadIssued) return;
        sceneLoadIssued = true;
        isLoading = true;
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
