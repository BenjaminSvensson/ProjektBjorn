using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu; // Assign your pause menu here
    private bool isPaused = false;

    void Update()
    {
        // Check if Escape key is pressed
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    private void TogglePause()
    {
        isPaused = !isPaused;

        if (pauseMenu != null)
            pauseMenu.SetActive(isPaused);

        Time.timeScale = isPaused ? 0f : 1f;
    }

    // Optional: Resume button in UI
    public void ResumeGame()
    {
        if (isPaused)
            TogglePause();
    }
}
