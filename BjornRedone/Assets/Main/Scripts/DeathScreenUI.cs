using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;

public class DeathScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent GameObject containing the death text and buttons.")]
    [SerializeField] private GameObject deathScreenPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    
    [Header("Settings")]
    [Tooltip("How many seconds to wait after death before showing the screen.")]
    [SerializeField] private float delayBeforeShowing = 1.5f;
    [Tooltip("Should the game pause (TimeScale = 0) when the screen appears?")]
    [SerializeField] private bool pauseTimeOnShow = true;
    [SerializeField] private float fadeInDuration = 0.4f;

    private CanvasGroup canvasGroup;
    private bool hasTriggered;

    void Awake()
    {
        // Fallback: If panel isn't assigned, try to find a child named "Panel" or just use the first child
        if (deathScreenPanel == null && transform.childCount > 0)
        {
            deathScreenPanel = transform.GetChild(0).gameObject;
        }

        if (deathScreenPanel)
        {
            canvasGroup = deathScreenPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = deathScreenPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            deathScreenPanel.SetActive(false);
        }
        else Debug.LogError("DeathScreenUI: No Death Screen Panel assigned or found!");
    }

    void Start()
    {
        if (restartButton) restartButton.onClick.AddListener(RestartGame);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
    }

    public void TriggerDeath()
    {
        if (hasTriggered) return;
        hasTriggered = true;
        StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        // Use Realtime so it works even if something else set TimeScale to 0
        yield return new WaitForSecondsRealtime(delayBeforeShowing);
        
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        if (deathScreenPanel)
        {
            deathScreenPanel.SetActive(true);
            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, fadeInDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = t * t * (3f - 2f * t);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
        
        if (pauseTimeOnShow)
        {
            Time.timeScale = 0f;
        }

        if (EventSystem.current != null && restartButton != null)
        {
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; 
        PlayerSceneState.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        PlayerSceneState.Clear();
        SceneManager.LoadScene("BjornMenu");
    }
}
