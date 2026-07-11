using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;

    private bool isPaused;
    private CanvasGroup pauseGroup;
    private RectTransform pauseCard;
    private Button resumeButton;
    private Coroutine transitionRoutine;

    public bool IsPaused => isPaused;

    void Awake()
    {
        if (pauseMenu == null) return;
        BuildPauseMenu();
        pauseMenu.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        if (pauseMenu == null) return;

        isPaused = !isPaused;
        if (isPaused)
        {
            pauseMenu.SetActive(true);
            Time.timeScale = 0f;
            AnimateMenu(true);
            SelectFirstControl();
        }
        else
        {
            Time.timeScale = 1f;
            GameSettings.Save();
            AnimateMenu(false);
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }

    public void ResumeGame()
    {
        if (isPaused) TogglePause();
    }

    public void RestartRun()
    {
        RestoreTime();
        PlayerSceneState.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        RestoreTime();
        PlayerSceneState.Clear();
        SceneManager.LoadScene("BjornMenu");
    }

    private void BuildPauseMenu()
    {
        foreach (Transform child in pauseMenu.transform) child.gameObject.SetActive(false);

        Canvas canvas = pauseMenu.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30050;
        }

        CanvasScaler scaler = pauseMenu.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        pauseGroup = pauseMenu.GetComponent<CanvasGroup>();
        if (pauseGroup == null) pauseGroup = pauseMenu.AddComponent<CanvasGroup>();

        GameObject backdrop = CreateImage(pauseMenu.transform, "Pause Backdrop", new Color(0f, 0f, 0f, 0.68f), false);
        Stretch(backdrop.GetComponent<RectTransform>());

        GameObject depth = CreateImage(pauseMenu.transform, "Pause Depth", new Color(0f, 0f, 0f, 0.72f), true);
        Anchor(depth.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, -13f), new Vector2(584f, 614f));

        GameObject cardObject = CreateImage(pauseMenu.transform, "Pause Card", new Color(0.025f, 0.025f, 0.03f, 0.97f), true);
        pauseCard = cardObject.GetComponent<RectTransform>();
        Anchor(pauseCard, Vector2.zero, Vector2.zero, new Vector2(560f, 590f));

        GameObject topMark = CreateImage(pauseCard, "Top Mark", Color.white, true);
        Anchor(topMark.GetComponent<RectTransform>(), Vector2.zero, new Vector2(-224f, 252f), new Vector2(48f, 3f));
        CreateText(pauseCard, "Title", "PAUSED", new Vector2(0f, 215f), new Vector2(464f, 48f), 32f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
        CreateText(pauseCard, "Subtitle", "The expedition is waiting.", new Vector2(0f, 180f), new Vector2(464f, 28f), 13f, TextAlignmentOptions.Left, new Color(0.58f, 0.58f, 0.62f, 1f));

        CreateText(pauseCard, "Audio Label", "QUICK AUDIO", new Vector2(0f, 125f), new Vector2(464f, 24f), 11f, TextAlignmentOptions.Left, new Color(0.64f, 0.64f, 0.68f, 1f), FontStyles.Bold);
        CreateSliderRow(pauseCard, "MASTER", 85f, GameSettings.MasterVolume, value => GameSettings.MasterVolume = value);
        CreateSliderRow(pauseCard, "MUSIC", 30f, GameSettings.MusicVolume, value => GameSettings.MusicVolume = value);

        resumeButton = CreateActionButton(pauseCard, "RESUME", new Vector2(0f, -55f), true, ResumeGame);
        CreateActionButton(pauseCard, "RESTART RUN", new Vector2(0f, -115f), false, RestartRun);
        CreateActionButton(pauseCard, "RETURN TO MENU", new Vector2(0f, -175f), false, ReturnToMainMenu);
        CreateText(pauseCard, "Hint", "ESC  RESUME", new Vector2(0f, -248f), new Vector2(464f, 30f), 12f, TextAlignmentOptions.Right, new Color(0.55f, 0.55f, 0.59f, 1f), FontStyles.Bold);

        pauseGroup.alpha = 0f;
        pauseGroup.interactable = false;
        pauseGroup.blocksRaycasts = false;
        pauseCard.localScale = Vector3.one * 0.94f;
    }

    private void CreateSliderRow(Transform parent, string label, float y, float value, UnityEngine.Events.UnityAction<float> changed)
    {
        GameObject row = CreateImage(parent, label + " Row", new Color(0.075f, 0.075f, 0.085f, 1f), true);
        Anchor(row.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, y), new Vector2(464f, 44f));
        CreateText(row.transform, "Label", label, new Vector2(-156f, 0f), new Vector2(120f, 44f), 12f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);

        GameObject sliderObject = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row.transform, false);
        Anchor(sliderObject.GetComponent<RectTransform>(), Vector2.zero, new Vector2(52f, 0f), new Vector2(236f, 26f));

        GameObject track = CreateImage(sliderObject.transform, "Track", new Color(1f, 1f, 1f, 0.14f), false);
        BjornUIStyle.ApplyPill(track.GetComponent<Image>());
        Stretch(track.GetComponent<RectTransform>(), new Vector2(0f, 9f), new Vector2(0f, -9f));
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(6f, 9f), new Vector2(-6f, -9f));
        GameObject fill = CreateImage(fillArea.transform, "Fill", Color.white, false);
        BjornUIStyle.ApplyPill(fill.GetComponent<Image>());
        Stretch(fill.GetComponent<RectTransform>());
        GameObject handleArea = new GameObject("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>(), new Vector2(9f, 0f), new Vector2(-9f, 0f));
        GameObject handle = CreateImage(handleArea.transform, "Handle", Color.white, false);
        BjornUIStyle.ApplyCircle(handle.GetComponent<Image>());
        Anchor(handle.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(18f, 18f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(changed);
    }

    private Button CreateActionButton(Transform parent, string label, Vector2 position, bool primary, UnityEngine.Events.UnityAction action)
    {
        Color background = primary ? Color.white : new Color(1f, 1f, 1f, 0.08f);
        GameObject buttonObject = CreateImage(parent, label, background, true);
        Anchor(buttonObject.GetComponent<RectTransform>(), Vector2.zero, position, new Vector2(464f, 46f));
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = primary ? new Color(0.88f, 0.88f, 0.9f, 1f) : new Color(1.5f, 1.5f, 1.5f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.72f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        CreateText(buttonObject.transform, "Label", label, Vector2.zero, new Vector2(430f, 46f), 13f, TextAlignmentOptions.Center, primary ? Color.black : Color.white, FontStyles.Bold);
        return button;
    }

    private void AnimateMenu(bool visible)
    {
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(AnimateMenuRoutine(visible));
    }

    private IEnumerator AnimateMenuRoutine(bool visible)
    {
        pauseGroup.interactable = visible;
        pauseGroup.blocksRaycasts = visible;
        float fromAlpha = pauseGroup.alpha;
        float toAlpha = visible ? 1f : 0f;
        Vector3 fromScale = pauseCard.localScale;
        Vector3 toScale = visible ? Vector3.one : Vector3.one * 0.96f;
        float elapsed = 0f;
        const float duration = 0.18f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            pauseGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, t);
            pauseCard.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        pauseGroup.alpha = toAlpha;
        pauseCard.localScale = toScale;
        if (!visible) pauseMenu.SetActive(false);
        transitionRoutine = null;
    }

    void OnDisable()
    {
        if (isPaused) RestoreTime();
    }

    private void RestoreTime()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        if (pauseMenu != null) pauseMenu.SetActive(false);
    }

    private void SelectFirstControl()
    {
        if (EventSystem.current != null && resumeButton != null)
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private static GameObject CreateImage(Transform parent, string name, Color color, bool rounded)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        if (rounded) BjornUIStyle.ApplyRounded(image);
        return imageObject;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        Anchor(textObject.GetComponent<RectTransform>(), Vector2.zero, position, size);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Anchor(RectTransform rect, Vector2 ignored, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, Vector2? offsetMin = null, Vector2? offsetMax = null)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin ?? Vector2.zero;
        rect.offsetMax = offsetMax ?? Vector2.zero;
    }
}
