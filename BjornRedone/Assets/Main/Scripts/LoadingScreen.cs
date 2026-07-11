using UnityEngine;
using System.Collections;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("The Panel containing the 'Loading...' text/image.")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TMP_Text statusText;

    [Header("Settings")]
    [Tooltip("How long to wait after generation before revealing the game (Realtime).")]
    [SerializeField] private float postGenerationDelay = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    private CanvasGroup canvasGroup;
    private Coroutine dismissRoutine;

    void Awake()
    {
        if (loadingPanel == null && transform.childCount > 0)
        {
            loadingPanel = transform.GetChild(0).gameObject;
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            if (statusText == null)
            {
                statusText = loadingPanel.GetComponentInChildren<TMP_Text>(true);
            }
        }

        Time.timeScale = 0f;
    }

    public void Prepare(string runCode)
    {
        if (dismissRoutine != null)
        {
            StopCoroutine(dismissRoutine);
            dismissRoutine = null;
        }

        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        Time.timeScale = 0f;
        SetStatus($"BUILDING CUDDLETOWN\nRUN {runCode}");
    }

    /// <summary>
    /// Called by LevelGenerator when the map is fully built.
    /// </summary>
    public void Dismiss()
    {
        SetStatus("DISTRICT READY");
        if (dismissRoutine != null) StopCoroutine(dismissRoutine);
        dismissRoutine = StartCoroutine(DismissRoutine());
    }

    public void ShowError(string message)
    {
        SetStatus(message);
    }

    private IEnumerator DismissRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Clamp(postGenerationDelay, 0f, 0.75f));

        Time.timeScale = 1f;

        if (canvasGroup != null && fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (loadingPanel) loadingPanel.SetActive(false);
        dismissRoutine = null;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
