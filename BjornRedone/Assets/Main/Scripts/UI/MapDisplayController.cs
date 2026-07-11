using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MapDisplayController : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.018f, 0.034f, 0.033f, 1f);
    private static readonly Color Cream = new Color(1f, 0.88f, 0.68f, 1f);
    private static readonly Color Accent = new Color(0.94f, 0.48f, 0.16f, 1f);
    private static readonly Color Muted = new Color(0.63f, 0.69f, 0.64f, 1f);

    private Canvas minimapCanvas;
    private Canvas shadowCanvas;
    private RawImage mapView;
    private RawImage mapFrame;
    private MinimapFollow minimapFollow;
    private LevelGenerator levelGenerator;
    private GameObject fullscreenRoot;
    private CanvasGroup fullscreenGroup;
    private RectTransform fullscreenMap;
    private Coroutine transitionRoutine;
    private bool isFullscreen;
    private float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }

    private static void Install(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "CuddleTown") return;
        GameObject mapUi = GameObject.Find("MapUi");
        if (mapUi != null && mapUi.GetComponent<MapDisplayController>() == null)
            mapUi.AddComponent<MapDisplayController>();
    }

    private void Awake()
    {
        minimapCanvas = GetComponent<Canvas>();
        GameObject shadow = GameObject.Find("MapShadow");
        shadowCanvas = shadow != null ? shadow.GetComponent<Canvas>() : null;
        mapView = transform.Find("MapShow")?.GetComponent<RawImage>();
        mapFrame = transform.Find("MapArt")?.GetComponent<RawImage>();
        minimapFollow = FindFirstObjectByType<MinimapFollow>();
        levelGenerator = FindFirstObjectByType<LevelGenerator>();

        ConfigureCompactMinimap();
        BuildFullscreenMap();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            SetFullscreen(!isFullscreen);
    }

    private void ConfigureCompactMinimap()
    {
        ConfigureMapScaler(GetComponent<CanvasScaler>());
        if (shadowCanvas != null) ConfigureMapScaler(shadowCanvas.GetComponent<CanvasScaler>());

        if (mapFrame != null)
        {
            RectTransform rect = mapFrame.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-22f, -22f);
            rect.sizeDelta = new Vector2(190f, 190f);
            mapFrame.raycastTarget = true;
            Button frameButton = mapFrame.GetComponent<Button>();
            if (frameButton == null) frameButton = mapFrame.gameObject.AddComponent<Button>();
            frameButton.onClick.AddListener(() => SetFullscreen(true));
        }

        if (mapView != null)
        {
            RectTransform rect = mapView.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-117f, -117f);
            rect.sizeDelta = new Vector2(190f, 190f);
            rect.localScale = Vector3.one * 0.86f;
        }

        if (shadowCanvas != null)
        {
            Image shadowImage = shadowCanvas.GetComponentInChildren<Image>(true);
            if (shadowImage != null)
            {
                RectTransform rect = shadowImage.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-16f, -16f);
                rect.sizeDelta = new Vector2(198f, 198f);
            }
        }

        GameObject hintPill = CreateImage(transform, "Map Hint", new Color(0.025f, 0.05f, 0.047f, 0.9f));
        RectTransform hintRect = hintPill.GetComponent<RectTransform>();
        Anchor(hintRect, new Vector2(1f, 1f), new Vector2(-117f, -226f), new Vector2(116f, 28f));
        CreateText(hintPill.transform, "Hint", "M   MAP", Vector2.zero, new Vector2(116f, 28f), 13f, TextAlignmentOptions.Center, Cream, FontStyles.Bold);
        Button mapButton = hintPill.AddComponent<Button>();
        mapButton.onClick.AddListener(() => SetFullscreen(true));
    }

    private void BuildFullscreenMap()
    {
        fullscreenRoot = new GameObject("Fullscreen Map", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = fullscreenRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31000;
        CanvasScaler scaler = fullscreenRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        fullscreenGroup = fullscreenRoot.GetComponent<CanvasGroup>();
        GameObject backdrop = CreateImage(fullscreenRoot.transform, "Backdrop", new Color(0.006f, 0.016f, 0.015f, 0.94f));
        Stretch(backdrop.GetComponent<RectTransform>());

        GameObject topRule = CreateImage(fullscreenRoot.transform, "Top Accent", Accent);
        RectTransform ruleRect = topRule.GetComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0f, 1f);
        ruleRect.anchorMax = new Vector2(1f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.sizeDelta = new Vector2(0f, 5f);
        ruleRect.anchoredPosition = Vector2.zero;

        CreateText(fullscreenRoot.transform, "Eyebrow", "CUDDLETOWN // SURVEY", new Vector2(0f, 462f), new Vector2(760f, 30f), 15f, TextAlignmentOptions.Center, Accent, FontStyles.Bold);
        CreateText(fullscreenRoot.transform, "Title", "EXPEDITION MAP", new Vector2(0f, 421f), new Vector2(900f, 58f), 38f, TextAlignmentOptions.Center, Cream, FontStyles.Bold);

        GameObject mapSurface = CreateImage(fullscreenRoot.transform, "Map Surface", new Color(0.05f, 0.075f, 0.07f, 1f));
        fullscreenMap = mapSurface.GetComponent<RectTransform>();
        Anchor(fullscreenMap, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(720f, 720f));
        Shadow mapShadow = mapSurface.AddComponent<Shadow>();
        mapShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        mapShadow.effectDistance = new Vector2(12f, -12f);

        if (mapView != null)
        {
            GameObject overview = new GameObject("Overview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            overview.transform.SetParent(fullscreenMap, false);
            RectTransform rect = overview.GetComponent<RectTransform>();
            Stretch(rect);
            rect.localScale = Vector3.one * 0.86f;
            RawImage image = overview.GetComponent<RawImage>();
            image.texture = mapView.texture;
            image.color = mapView.color;
            image.raycastTarget = false;
        }

        if (mapFrame != null)
        {
            GameObject frame = new GameObject("Parchment Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            frame.transform.SetParent(fullscreenMap, false);
            Stretch(frame.GetComponent<RectTransform>());
            RawImage image = frame.GetComponent<RawImage>();
            image.texture = mapFrame.texture;
            image.color = mapFrame.color;
            image.raycastTarget = false;
        }

        string runCode = levelGenerator != null ? levelGenerator.RunCode : "--------";
        CreateText(fullscreenRoot.transform, "Run", "RUN " + runCode, new Vector2(-300f, -420f), new Vector2(360f, 38f), 15f, TextAlignmentOptions.Left, Muted, FontStyles.Bold);
        GameObject closeObject = CreateImage(fullscreenRoot.transform, "Close Map", Color.clear);
        Anchor(closeObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(300f, -420f), new Vector2(360f, 38f));
        CreateText(closeObject.transform, "Label", "M   CLOSE MAP", Vector2.zero, new Vector2(360f, 38f), 15f, TextAlignmentOptions.Right, Cream, FontStyles.Bold);
        Button closeButton = closeObject.AddComponent<Button>();
        closeButton.onClick.AddListener(() => SetFullscreen(false));

        fullscreenGroup.alpha = 0f;
        fullscreenGroup.interactable = false;
        fullscreenGroup.blocksRaycasts = false;
        fullscreenMap.localScale = Vector3.one * 0.92f;
        fullscreenRoot.SetActive(false);
    }

    private void SetFullscreen(bool visible)
    {
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(AnimateFullscreen(visible));
    }

    private IEnumerator AnimateFullscreen(bool visible)
    {
        isFullscreen = visible;
        if (visible)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            minimapCanvas.enabled = false;
            if (shadowCanvas != null) shadowCanvas.enabled = false;
            if (levelGenerator == null) levelGenerator = FindFirstObjectByType<LevelGenerator>();
            if (minimapFollow == null) minimapFollow = FindFirstObjectByType<MinimapFollow>();
            if (levelGenerator != null && minimapFollow != null)
                minimapFollow.ShowOverview(levelGenerator.GeneratedWorldBounds);
            fullscreenRoot.SetActive(true);
        }

        fullscreenGroup.interactable = visible;
        fullscreenGroup.blocksRaycasts = visible;
        float from = fullscreenGroup.alpha;
        float to = visible ? 1f : 0f;
        Vector3 fromScale = fullscreenMap.localScale;
        Vector3 toScale = visible ? Vector3.one : Vector3.one * 0.96f;
        if (visible && from <= 0.01f) fromScale = Vector3.one * 0.92f;

        float elapsed = 0f;
        const float duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            fullscreenGroup.alpha = Mathf.LerpUnclamped(from, to, eased);
            fullscreenMap.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            yield return null;
        }

        fullscreenGroup.alpha = to;
        fullscreenMap.localScale = toScale;
        if (!visible)
        {
            minimapFollow?.ShowPlayerFollow();
            fullscreenRoot.SetActive(false);
            if (minimapCanvas != null) minimapCanvas.enabled = true;
            if (shadowCanvas != null) shadowCanvas.enabled = true;
            Time.timeScale = previousTimeScale;
        }
        transitionRoutine = null;
    }

    private static void ConfigureMapScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static GameObject CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return imageObject;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        Anchor(textObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, size);
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
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (isFullscreen)
        {
            minimapFollow?.ShowPlayerFollow();
            Time.timeScale = previousTimeScale;
        }
        if (fullscreenRoot != null) Destroy(fullscreenRoot);
    }
}
