using UnityEngine;

public class TriggerUI : MonoBehaviour
{
    // We make this private because the script finds it automatically
    private GameObject uiCanvas; 

    [Header("Settings")]
    public string targetCanvasName = "DealerUi"; // The name to search for
    public string targetTag = "Player";
    public bool closeOnExit = true;

    private void Start()
    {
        // 1. Find the object by name
        uiCanvas = GameObject.Find(targetCanvasName);

        // 2. Check if we found it
        if (uiCanvas != null)
        {
            // 3. Hide it immediately so it's ready for gameplay
            uiCanvas.SetActive(false);
        }
        else
        {
            Debug.LogError($"Could not find an object named '{targetCanvasName}'. Make sure it is enabled in the scene!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(targetTag) && uiCanvas != null)
        {
            uiCanvas.SetActive(true);
            Debug.Log("Player entered trigger - UI Enabled");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (closeOnExit && other.CompareTag(targetTag) && uiCanvas != null)
        {
            uiCanvas.SetActive(false);
        }
    }
}