using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("The Panel containing the 'Loading...' text/image.")]
    [SerializeField] private GameObject loadingPanel;

    [Header("Settings")]
    [Tooltip("How long to wait after generation before revealing the game (Realtime).")]
    [SerializeField] private float postGenerationDelay = 0.2f;

    void Awake()
    {
        // 1. Activate Screen
        if (loadingPanel) loadingPanel.SetActive(true);
        
        // 2. Pause Time immediately so nothing moves/spawns logic doesn't run physics
    }

    /// <summary>
    /// Called by LevelGenerator when the map is fully built.
    /// </summary>
    public void Dismiss()
    {
        StartCoroutine(DismissRoutine());
    }

    private IEnumerator DismissRoutine()
    {
        // 3. Wait a split second (Realtime, since timescale is 0)
        yield return new WaitForSecondsRealtime(postGenerationDelay);

        // 4. Resume Game
        Time.timeScale = 1f;

        // 5. Hide Screen
        if (loadingPanel) loadingPanel.SetActive(false);
    }
}