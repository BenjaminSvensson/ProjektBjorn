using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class UIInteractionPulse : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.045f;
    [SerializeField] private float pressedScale = 0.965f;
    [SerializeField] private float responseSpeed = 18f;

    private Vector3 restingScale;
    private bool isHovered;
    private bool isSelected;
    private bool isPressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= InstallOnSceneButtons;
        SceneManager.sceneLoaded += InstallOnSceneButtons;
    }

    private static void InstallOnSceneButtons(Scene scene, LoadSceneMode mode)
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.GetComponent<UIInteractionPulse>() == null)
            {
                button.gameObject.AddComponent<UIInteractionPulse>();
            }
        }

        CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CanvasScaler scaler in scalers)
        {
            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) continue;
            if (canvas != null && canvas.overrideSorting && canvas.sortingOrder >= 30000) continue;

            bool isMapCanvas = scaler.name == "MapUi" || scaler.name == "MapShadow";
            if (isMapCanvas)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                continue;
            }

            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize) continue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    void Awake()
    {
        restingScale = transform.localScale;
    }

    void OnEnable()
    {
        if (restingScale == Vector3.zero) restingScale = transform.localScale;
        isHovered = false;
        isSelected = false;
        isPressed = false;
    }

    void Update()
    {
        float scaleMultiplier = isPressed ? pressedScale : (isHovered || isSelected ? hoverScale : 1f);
        Vector3 target = restingScale * scaleMultiplier;
        float blend = 1f - Mathf.Exp(-responseSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, target, blend);
    }

    void OnDisable()
    {
        if (restingScale != Vector3.zero) transform.localScale = restingScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovered = true;
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;

        // Pointer hover should never leave a button looking permanently focused.
        // Keyboard/gamepad navigation can select it again on the next navigation input.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            isSelected = false;
            EventSystem.current.SetSelectedGameObject(null, eventData);
        }
    }

    public void OnPointerDown(PointerEventData eventData) => isPressed = true;
    public void OnPointerUp(PointerEventData eventData) => isPressed = false;
    public void OnSelect(BaseEventData eventData) => isSelected = true;
    public void OnDeselect(BaseEventData eventData) => isSelected = false;
}
