using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Settingsscript : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private GameObject objectToToggle;
    [SerializeField] private float animationDuration = 0.22f;

    private static readonly Color Ink = new Color(0.015f, 0.015f, 0.018f, 1f);
    private static readonly Color Surface = new Color(0.025f, 0.025f, 0.03f, 0.97f);
    private static readonly Color Raised = new Color(0.075f, 0.075f, 0.085f, 1f);
    private static readonly Color Accent = new Color(1f, 1f, 1f, 1f);
    private static readonly Color Cream = new Color(1f, 1f, 1f, 1f);
    private static readonly Color Text = new Color(0.96f, 0.96f, 0.97f, 1f);
    private static readonly Color Muted = new Color(0.58f, 0.58f, 0.62f, 1f);

    private CanvasGroup canvasGroup;
    private RectTransform card;
    private Coroutine animationRoutine;
    private Slider masterSlider;
    private Slider musicSlider;
    private Slider shakeSlider;
    private TMP_Text fullscreenState;
    private TMP_Text vsyncState;
    private bool fullscreen;
    private bool vsync;

    private void Awake()
    {
        if (toggleButton == null) toggleButton = GetComponent<Button>();
        if (objectToToggle == null) return;

        bool initiallyVisible = objectToToggle.activeSelf;
        canvasGroup = objectToToggle.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = objectToToggle.AddComponent<CanvasGroup>();

        BuildOptionsPanel();
        canvasGroup.alpha = initiallyVisible ? 1f : 0f;
        canvasGroup.interactable = initiallyVisible;
        canvasGroup.blocksRaycasts = initiallyVisible;

        if (toggleButton != null) toggleButton.onClick.AddListener(ToggleObject);
    }

    private void Update()
    {
        if (objectToToggle != null && objectToToggle.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetVisible(false);
        }
    }

    private void ToggleObject()
    {
        if (objectToToggle != null) SetVisible(!objectToToggle.activeSelf);
    }

    private void SetVisible(bool visible)
    {
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateVisibility(visible));
    }

    private IEnumerator AnimateVisibility(bool visible)
    {
        SceneMenuManager menu = FindFirstObjectByType<SceneMenuManager>();
        if (visible)
        {
            RefreshControls();
            objectToToggle.SetActive(true);
            menu?.SetMenuControlsVisible(false);
        }

        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        float fromAlpha = canvasGroup.alpha;
        float toAlpha = visible ? 1f : 0f;
        Vector3 fromScale = card != null ? card.localScale : Vector3.one;
        Vector3 toScale = visible ? Vector3.one : Vector3.one * 0.97f;
        if (visible && fromAlpha <= 0.01f) fromScale = Vector3.one * 0.94f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
            if (card != null) card.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
        if (card != null) card.localScale = toScale;

        if (!visible)
        {
            objectToToggle.SetActive(false);
            menu?.SetMenuControlsVisible(true);
            GameSettings.Save();
            EventSystem.current?.SetSelectedGameObject(toggleButton != null ? toggleButton.gameObject : null);
        }
        else
        {
            EventSystem.current?.SetSelectedGameObject(masterSlider != null ? masterSlider.gameObject : null);
        }

        animationRoutine = null;
    }

    private void BuildOptionsPanel()
    {
        foreach (Transform child in objectToToggle.transform)
        {
            child.gameObject.SetActive(false);
        }

        Canvas canvas = objectToToggle.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
        }

        CanvasScaler scaler = objectToToggle.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject backdrop = CreateImage(objectToToggle.transform, "Options Backdrop", new Color(0f, 0f, 0f, 0.86f));
        Stretch(backdrop.GetComponent<RectTransform>());
        Button closeBackdrop = backdrop.AddComponent<Button>();
        closeBackdrop.transition = Selectable.Transition.None;
        closeBackdrop.onClick.AddListener(() => SetVisible(false));

        GameObject shadowObject = CreateRoundedImage(objectToToggle.transform, "Options Depth", new Color(0f, 0f, 0f, 0.66f));
        Anchor(shadowObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(724f, 454f));

        GameObject cardObject = CreateRoundedImage(objectToToggle.transform, "Options Card", Surface);
        card = cardObject.GetComponent<RectTransform>();
        Anchor(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 430f));

        GameObject topMark = CreateRoundedImage(card, "Top Mark", Color.white);
        Anchor(topMark.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-281f, 185f), new Vector2(48f, 3f));
        CreateText(card, "Title", "OPTIONS", new Vector2(0f, 153f), new Vector2(610f, 46f), 30f, TextAlignmentOptions.Left, Cream, FontStyles.Bold);
        CreateText(card, "Subtitle", "Audio, display and game feel", new Vector2(0f, 119f), new Vector2(610f, 24f), 13f, TextAlignmentOptions.Left, Muted);

        CreateSectionLabel(card, "AUDIO", -175f, 80f);
        masterSlider = CreateSliderRow(card, "MASTER VOLUME", -175f, 34f, GameSettings.MasterVolume, value => GameSettings.MasterVolume = value);
        musicSlider = CreateSliderRow(card, "MUSIC", -175f, -46f, GameSettings.MusicVolume, value => GameSettings.MusicVolume = value);

        CreateSectionLabel(card, "DISPLAY", 175f, 80f);
        CreateToggleRow(card, "FULLSCREEN", 175f, 34f, () => fullscreen, value =>
        {
            fullscreen = value;
            GameSettings.Fullscreen = value;
            UpdateToggleText(fullscreenState, value);
        }, out fullscreenState);
        CreateToggleRow(card, "VERTICAL SYNC", 175f, -20f, () => vsync, value =>
        {
            vsync = value;
            GameSettings.VSync = value;
            UpdateToggleText(vsyncState, value);
        }, out vsyncState);
        CreateSectionLabel(card, "GAME FEEL", 175f, -72f);
        shakeSlider = CreateSliderRow(card, "SCREEN SHAKE", 175f, -122f, GameSettings.ScreenShake, value => GameSettings.ScreenShake = value);

        Button reset = CreateTextButton(card, "Restore Defaults", "RESTORE DEFAULTS", new Vector2(-210f, -191f), new Vector2(230f, 36f));
        reset.onClick.AddListener(ResetDefaults);
        CreateText(card, "Close Hint", "ESC  CLOSE", new Vector2(235f, -191f), new Vector2(180f, 36f), 13f, TextAlignmentOptions.Right, Muted, FontStyles.Bold);

        RefreshControls();
    }

    private void RefreshControls()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (shakeSlider != null) shakeSlider.SetValueWithoutNotify(GameSettings.ScreenShake);
        RefreshSliderReadout(masterSlider);
        RefreshSliderReadout(musicSlider);
        RefreshSliderReadout(shakeSlider);

        fullscreen = GameSettings.Fullscreen;
        vsync = GameSettings.VSync;
        UpdateToggleText(fullscreenState, fullscreen);
        UpdateToggleText(vsyncState, vsync);
    }

    private void ResetDefaults()
    {
        GameSettings.ResetToDefaults();
        RefreshControls();
    }

    private Slider CreateSliderRow(Transform parent, string label, float x, float y, float value, Action<float> changed)
    {
        GameObject row = CreateRoundedImage(parent, label + " Row", Raised);
        Anchor(row.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(300f, 70f));
        CreateText(row.transform, "Label", label, new Vector2(0f, 19f), new Vector2(260f, 26f), 13f, TextAlignmentOptions.Left, Text, FontStyles.Bold);

        GameObject sliderObject = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        Anchor(sliderRect, new Vector2(0.5f, 0.5f), new Vector2(-19f, -14f), new Vector2(214f, 26f));

        GameObject track = CreateImage(sliderRect, "Track", new Color(1f, 1f, 1f, 0.13f));
        BjornUIStyle.ApplyPill(track.GetComponent<Image>());
        Stretch(track.GetComponent<RectTransform>(), new Vector2(0f, 9f), new Vector2(-1f, -9f));

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderRect, false);
        Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(7f, 9f), new Vector2(-7f, -9f));
        GameObject fill = CreateImage(fillArea.transform, "Fill", Accent);
        BjornUIStyle.ApplyPill(fill.GetComponent<Image>());
        Stretch(fill.GetComponent<RectTransform>());

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderRect, false);
        Stretch(handleArea.GetComponent<RectTransform>(), new Vector2(10f, 0f), new Vector2(-10f, 0f));
        GameObject handle = CreateImage(handleArea.transform, "Handle", Cream);
        BjornUIStyle.ApplyCircle(handle.GetComponent<Image>());
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        Anchor(handleRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.SetValueWithoutNotify(value);

        TMP_Text readout = CreateText(row.transform, "Value", Mathf.RoundToInt(value * 100f) + "%", new Vector2(122f, -14f), new Vector2(48f, 26f), 12f, TextAlignmentOptions.Right, Cream, FontStyles.Bold);
        slider.onValueChanged.AddListener(newValue =>
        {
            readout.text = Mathf.RoundToInt(newValue * 100f) + "%";
            changed(newValue);
        });
        return slider;
    }

    private void CreateToggleRow(Transform parent, string label, float x, float y, Func<bool> getValue, Action<bool> setValue, out TMP_Text stateText)
    {
        GameObject row = CreateRoundedImage(parent, label + " Row", Raised);
        Anchor(row.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(300f, 42f));
        CreateText(row.transform, "Label", label, new Vector2(-57f, 0f), new Vector2(160f, 42f), 13f, TextAlignmentOptions.Left, Text, FontStyles.Bold);

        Button button = CreateTextButton(row.transform, label + " Toggle", string.Empty, new Vector2(103f, 0f), new Vector2(74f, 28f));
        stateText = button.GetComponentInChildren<TMP_Text>();
        TMP_Text capturedState = stateText;
        button.onClick.AddListener(() =>
        {
            bool next = !getValue();
            setValue(next);
            UpdateToggleText(capturedState, next);
        });
    }

    private static void UpdateToggleText(TMP_Text text, bool enabled)
    {
        if (text == null) return;
        text.text = enabled ? "ON" : "OFF";
        text.color = enabled ? Cream : Muted;
        Image background = text.transform.parent.GetComponent<Image>();
        if (background != null) background.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.08f);
        if (enabled) text.color = Color.black;
    }

    private static void RefreshSliderReadout(Slider slider)
    {
        if (slider == null) return;
        Transform value = slider.transform.parent.Find("Value");
        if (value != null && value.TryGetComponent(out TMP_Text readout))
            readout.text = Mathf.RoundToInt(slider.value * 100f) + "%";
    }

    private static void CreateSectionLabel(Transform parent, string value, float x, float y)
    {
        CreateText(parent, value + " Section", value, new Vector2(x, y), new Vector2(300f, 24f), 11f, TextAlignmentOptions.Left, Accent, FontStyles.Bold);
    }

    private static Button CreateTextButton(Transform parent, string name, string value, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = CreateRoundedImage(parent, name, new Color(1f, 1f, 1f, 0.08f));
        Anchor(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, size);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.74f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        CreateText(buttonObject.transform, "Text", value, Vector2.zero, size, 13f, TextAlignmentOptions.Center, Cream, FontStyles.Bold);
        return button;
    }

    private static GameObject CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return imageObject;
    }

    private static GameObject CreateRoundedImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = CreateImage(parent, name, color);
        BjornUIStyle.ApplyRounded(imageObject.GetComponent<Image>());
        return imageObject;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(0.5f, 0.5f), position, size);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
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

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleObject);
        GameSettings.Save();
    }
}
