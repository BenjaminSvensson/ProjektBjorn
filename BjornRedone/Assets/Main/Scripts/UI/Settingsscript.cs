using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class Settingsscript : MonoBehaviour
{
    [SerializeField] private Button toggleButton;      // The button to listen to
    [SerializeField] private GameObject objectToToggle; // The object to show/hide
    [SerializeField] private float animationDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private Coroutine animationRoutine;

    private void Awake()
    {
        if (toggleButton == null)
            toggleButton = GetComponent<Button>();

        if (objectToToggle != null)
        {
            canvasGroup = objectToToggle.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = objectToToggle.AddComponent<CanvasGroup>();
            StyleSettingsPanel();
        }

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleObject);
    }

    private void Update()
    {
        if (objectToToggle != null && objectToToggle.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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
        if (visible) objectToToggle.SetActive(true);

        float from = canvasGroup != null ? canvasGroup.alpha : (visible ? 0f : 1f);
        float to = visible ? 1f : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = to;
            canvasGroup.blocksRaycasts = visible;
        }

        if (!visible)
        {
            objectToToggle.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(toggleButton != null ? toggleButton.gameObject : null);
        }
        else if (EventSystem.current != null)
        {
            Selectable firstControl = objectToToggle.GetComponentInChildren<Selectable>(true);
            EventSystem.current.SetSelectedGameObject(firstControl != null ? firstControl.gameObject : null);
        }

        animationRoutine = null;
    }

    private void StyleSettingsPanel()
    {
        Image[] images = objectToToggle.GetComponentsInChildren<Image>(true);
        Image backdrop = null;
        float largestArea = 0f;

        foreach (Image image in images)
        {
            RectTransform rect = image.rectTransform;
            float area = Mathf.Abs(rect.sizeDelta.x * rect.sizeDelta.y);
            if (area > largestArea)
            {
                largestArea = area;
                backdrop = image;
            }

            string lowerName = image.name.ToLowerInvariant();
            if (lowerName.Contains("fill")) image.color = new Color(0.93f, 0.52f, 0.22f, 1f);
            else if (lowerName.Contains("handle")) image.color = new Color(1f, 0.88f, 0.68f, 1f);
            else if (lowerName.Contains("background")) image.color = new Color(0.12f, 0.16f, 0.17f, 0.95f);
        }

        if (backdrop != null) backdrop.color = new Color(0.025f, 0.04f, 0.045f, 0.9f);

        CreatePanelSurfaces();

        TMP_Text[] labels = objectToToggle.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label.text.Trim().Equals("Music", System.StringComparison.OrdinalIgnoreCase))
            {
                label.text = "MUSIC";
                label.fontSize = 22f;
                label.color = new Color(0.93f, 0.93f, 0.88f, 1f);
            }
        }

        Slider slider = objectToToggle.GetComponentInChildren<Slider>(true);
        if (slider != null)
        {
            RectTransform sliderRect = slider.transform as RectTransform;
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, -5f);
            sliderRect.sizeDelta = new Vector2(360f, 34f);
        }

        if (objectToToggle.transform.Find("CodexOptionsTitle") == null)
        {
            CreatePanelLabel("CodexOptionsTitle", "OPTIONS", new Vector2(0f, 115f), 42f, new Color(1f, 0.86f, 0.66f, 1f));
            CreatePanelLabel("CodexOptionsHint", "[ESC]  CLOSE", new Vector2(0f, -125f), 16f, new Color(0.68f, 0.72f, 0.7f, 1f));
        }
    }

    private void CreatePanelLabel(string objectName, string value, Vector2 position, float fontSize, Color color)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(objectToToggle.transform, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(500f, 64f);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
    }

    private void CreatePanelSurfaces()
    {
        if (objectToToggle.transform.Find("CodexOptionsBackdrop") != null) return;

        GameObject backdropObject = new GameObject("CodexOptionsBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backdropObject.transform.SetParent(objectToToggle.transform, false);
        backdropObject.transform.SetAsFirstSibling();
        RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdropObject.GetComponent<Image>();
        backdropImage.color = new Color(0.01f, 0.018f, 0.02f, 0.78f);
        backdropImage.raycastTarget = true;

        GameObject cardObject = new GameObject("CodexOptionsCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObject.transform.SetParent(objectToToggle.transform, false);
        cardObject.transform.SetSiblingIndex(1);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(480f, 320f);
        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.045f, 0.07f, 0.075f, 0.97f);
        cardImage.raycastTarget = true;
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleObject);
    }
}
