using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

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

    void Awake()
    {
        // Fallback: If panel isn't assigned, try to find a child named "Panel" or just use the first child
        if (deathScreenPanel == null && transform.childCount > 0)
        {
            deathScreenPanel = transform.GetChild(0).gameObject;
        }

        if (deathScreenPanel) deathScreenPanel.SetActive(false);
        else Debug.LogError("DeathScreenUI: No Death Screen Panel assigned or found!");
    }

    void Start()
    {
        if (restartButton) restartButton.onClick.AddListener(RestartGame);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
    }

    public void TriggerDeath()
    {
        Debug.Log("DeathScreenUI: TriggerDeath called. Waiting " + delayBeforeShowing + " seconds.");
        StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        // Use Realtime so it works even if something else set TimeScale to 0
        yield return new WaitForSecondsRealtime(delayBeforeShowing);
        
        Debug.Log("DeathScreenUI: Showing Screen.");

        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        if (deathScreenPanel) deathScreenPanel.SetActive(true);
        
        if (pauseTimeOnShow)
        {
            Time.timeScale = 0f;
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; 
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
}