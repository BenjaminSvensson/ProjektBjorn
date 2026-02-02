using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Needed for Image
using System.Collections;

public class BossWatcher : MonoBehaviour
{
    [Header("Scene Settings")]
    public string creditsSceneName = "Credits";

    [Header("UI Connections")]
    [Tooltip("Drag your black Panel here. It must have an Image component.")]
    public Image fadePanelImage;

    [Header("Timing")]
    public float waitBeforeFade = 3.0f; 
    public float fadeDuration = 2.0f;

    // Internal state
    private GameObject activeBoss;
    private bool hasFoundBoss = false;
    private bool sequenceStarted = false;

    void Start()
    {
        // Ensure the screen starts clear/invisible
        if (fadePanelImage != null)
        {
            Color c = fadePanelImage.color;
            c.a = 0f;
            fadePanelImage.color = c;
            
            // Make sure it doesn't block mouse clicks while invisible
            fadePanelImage.raycastTarget = false; 
        }
    }

    void Update()
    {
        if (sequenceStarted) return;

        // PHASE 1: SEARCH FOR BOSS
        if (!hasFoundBoss)
        {
            // Try to find the penguin by his unique script
            PenguinEnemyAI bossScript = FindObjectOfType<PenguinEnemyAI>();
            
            if (bossScript != null)
            {
                activeBoss = bossScript.gameObject;
                hasFoundBoss = true;
                Debug.Log("BossWatcher: Boss found! Locked onto target.");
            }
        }
        // PHASE 2: WATCH FOR DEATH
        else 
        {
            // If we found him before, but now the variable is null, he was Destroyed.
            if (activeBoss == null)
            {
                Debug.Log("BossWatcher: Boss signal lost (Destroyed). Starting Ending.");
                StartCoroutine(EndGameSequence());
            }
        }
    }

    IEnumerator EndGameSequence()
    {
        sequenceStarted = true;

        // 1. Victory waiting time
        yield return new WaitForSeconds(waitBeforeFade);

        // 2. Fade Image to Black
        if (fadePanelImage != null)
        {
            // Block clicks now that we are fading out
            fadePanelImage.raycastTarget = true; 

            float timer = 0f;
            Color startColor = fadePanelImage.color;
            Color targetColor = new Color(0, 0, 0, 1); // Solid Black

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float t = timer / fadeDuration;
                
                // Fade the alpha
                Color newColor = startColor;
                newColor.a = Mathf.Lerp(0f, 1f, t);
                fadePanelImage.color = newColor;
                
                yield return null;
            }
            
            // Ensure fully black at end
            fadePanelImage.color = targetColor;
        }

        // 3. Load Credits
        SceneManager.LoadScene(creditsSceneName);
    }
}