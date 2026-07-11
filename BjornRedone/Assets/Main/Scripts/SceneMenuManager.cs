using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

public class SceneMenuManager : MonoBehaviour
{
    [Header("Button Configuration")]
    [Tooltip("Add your buttons here and type the exact name of the scene they should load.")]
    public List<SceneLink> menuButtons;

    [Header("Satisfying Audio Settings")]
    public AudioSource uiAudioSource;
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Range(0f, 0.2f)]
    public float pitchRandomization = 0.1f;

    [Header("Presentation")]
    [SerializeField] private float transitionDuration = 0.45f;
    [SerializeField] private float entranceDuration = 0.5f;
    [SerializeField] private Vector2 menuReferenceResolution = new Vector2(800f, 600f);

    private readonly List<Button> configuredButtons = new List<Button>();
    private CanvasGroup transitionOverlay;
    private bool isTransitioning;

    [System.Serializable]
    public struct SceneLink
    {
        public Button button;
        public string sceneName;
    }

    void Start()
    {
        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
        }

        ConfigureResponsiveCanvases();
        CreateTransitionOverlay();

        foreach (var link in menuButtons)
        {
            if (link.button != null)
            {
                SetupButton(link.button, link.sceneName);
                configuredButtons.Add(link.button);
            }
        }

        GameObject farBackground = GameObject.Find("FarBackGround");
        if (farBackground != null && farBackground.GetComponent<MenuParallaxMotion>() == null)
        {
            farBackground.AddComponent<MenuParallaxMotion>();
        }

        StartCoroutine(PlayMenuEntrance());
    }

    void SetupButton(Button btn, string sceneToLoad)
    {
        if (!string.IsNullOrWhiteSpace(sceneToLoad))
        {
            btn.onClick.AddListener(() => BeginLoadScene(sceneToLoad));
        }

        EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => { PlaySound(hoverSound); });
        
        trigger.triggers.Add(entry);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || uiAudioSource == null) return;

        uiAudioSource.pitch = 1f + Random.Range(-pitchRandomization, pitchRandomization);
        uiAudioSource.PlayOneShot(clip);
    }

    private void BeginLoadScene(string sceneName)
    {
        if (isTransitioning) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' cannot be loaded. Check your Build Settings!");
            return;
        }

        PlaySound(clickSound);
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;
        foreach (Button button in configuredButtons)
        {
            if (button != null) button.interactable = false;
        }

        if (transitionOverlay != null)
        {
            transitionOverlay.blocksRaycasts = true;
            yield return FadeCanvasGroup(transitionOverlay, transitionOverlay.alpha, 1f, transitionDuration);
        }

        if (SceneManager.GetActiveScene().name == "BjornMenu") PlayerSceneState.Clear();
        else if (!PlayerSceneState.SaveCurrentPlayer()) PlayerSceneState.Clear();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        if (loadOperation == null)
        {
            isTransitioning = false;
            yield break;
        }

        while (!loadOperation.isDone) yield return null;
    }

    private IEnumerator PlayMenuEntrance()
    {
        yield return null;

        if (transitionOverlay != null)
        {
            yield return FadeCanvasGroup(transitionOverlay, 1f, 0f, transitionDuration);
            transitionOverlay.blocksRaycasts = false;
        }

        for (int i = 0; i < configuredButtons.Count; i++)
        {
            Button button = configuredButtons[i];
            if (button == null) continue;
            StartCoroutine(AnimateButtonEntrance(button, i * 0.07f));
        }

        if (configuredButtons.Count > 0 && configuredButtons[0] != null)
        {
            EventSystem.current?.SetSelectedGameObject(configuredButtons[0].gameObject);
        }
    }

    private IEnumerator AnimateButtonEntrance(Button button, float delay)
    {
        RectTransform rect = button.transform as RectTransform;
        if (rect == null) yield break;

        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();

        Vector2 destination = rect.anchoredPosition;
        Vector2 origin = destination + Vector2.right * 24f;
        group.alpha = 0f;
        rect.anchoredPosition = origin;

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, entranceDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            group.alpha = eased;
            rect.anchoredPosition = Vector2.LerpUnclamped(origin, destination, eased);
            yield return null;
        }

        group.alpha = 1f;
        rect.anchoredPosition = destination;
    }

    private void ConfigureResponsiveCanvases()
    {
        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CanvasScaler scaler in scalers)
        {
            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) continue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = menuReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void CreateTransitionOverlay()
    {
        GameObject overlayObject = new GameObject("Menu Transition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = overlayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = overlayObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = menuReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject veil = new GameObject("Veil", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        veil.transform.SetParent(overlayObject.transform, false);
        RectTransform veilRect = veil.GetComponent<RectTransform>();
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
        veil.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.04f, 1f);

        transitionOverlay = overlayObject.GetComponent<CanvasGroup>();
        transitionOverlay.alpha = 1f;
        transitionOverlay.blocksRaycasts = true;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            group.alpha = Mathf.LerpUnclamped(from, to, t);
            yield return null;
        }
        group.alpha = to;
    }
}

public sealed class MenuParallaxMotion : MonoBehaviour
{
    [SerializeField] private Vector2 maxOffset = new Vector2(10f, 6f);
    [SerializeField] private float responsiveness = 3.5f;

    private RectTransform rect;
    private Vector2 restingPosition;

    void Awake()
    {
        rect = transform as RectTransform;
        if (rect != null) restingPosition = rect.anchoredPosition;
    }

    void LateUpdate()
    {
        if (rect == null) return;

        Vector2 normalizedPointer = Vector2.zero;
        if (Mouse.current != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            normalizedPointer = new Vector2(pointer.x / Screen.width, pointer.y / Screen.height) * 2f - Vector2.one;
        }

        Vector2 breathing = new Vector2(Mathf.Sin(Time.unscaledTime * 0.24f), Mathf.Cos(Time.unscaledTime * 0.19f)) * 1.5f;
        Vector2 target = restingPosition - Vector2.Scale(normalizedPointer, maxOffset) + breathing;
        float blend = 1f - Mathf.Exp(-responsiveness * Time.unscaledDeltaTime);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, blend);
    }
}
