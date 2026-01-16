using UnityEngine;

public class QuitButton : MonoBehaviour
{
    // Call this function from your Button's OnClick event
    public void QuitGame()
    {
        Debug.Log("Quit Game triggered!"); // This shows in the console so you know it worked

        // If we are running in the Unity Editor
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // If we are running in a standalone build
            Application.Quit();
        #endif
    }
}