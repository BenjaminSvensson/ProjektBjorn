using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    private bool isPaused;

    public bool IsPaused => isPaused;

    void Awake()
    {
        if (pauseMenu != null) pauseMenu.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (pauseMenu == null) return;

        isPaused = !isPaused;
        pauseMenu.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;

        if (isPaused)
        {
            SelectFirstControl();
        }
        else
        {
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

    void OnDisable()
    {
        if (isPaused) RestoreTime();
    }

    private void RestoreTime()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseMenu != null) pauseMenu.SetActive(false);
    }

    private void SelectFirstControl()
    {
        if (EventSystem.current == null || pauseMenu == null) return;

        Selectable firstControl = pauseMenu.GetComponentInChildren<Selectable>(true);
        EventSystem.current.SetSelectedGameObject(firstControl != null ? firstControl.gameObject : null);
    }
}
