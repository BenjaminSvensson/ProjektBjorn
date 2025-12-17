using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem; // Required for the New Input System

public class IngameDebugConsole : MonoBehaviour
{
    struct Log
    {
        public string message;
        public string stackTrace;
        public LogType type;
    }

    [Header("Settings")]
    [Tooltip("Press this key to open/close the console.")]
    [SerializeField] private Key toggleKey = Key.Backquote; // Changed from KeyCode to Key for Input System
    [Tooltip("Max number of lines to keep in memory.")]
    [SerializeField] private int maxLogCount = 50;
    [Tooltip("If true, the console won't destroy itself when loading new scenes.")]
    [SerializeField] private bool persistBetweenScenes = true;

    // State
    private List<Log> logs = new List<Log>();
    private Vector2 scrollPosition;
    private bool showConsole = false;
    
    // GUI Styles
    private GUIStyle logStyle;
    private GUIStyle warningStyle;
    private GUIStyle errorStyle;
    private bool stylesInitialized = false;

    void Awake()
    {
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void Update()
    {
        // Updated to use the New Input System
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            showConsole = !showConsole;
        }
    }

    void HandleLog(string message, string stackTrace, LogType type)
    {
        logs.Add(new Log { message = message, stackTrace = stackTrace, type = type });

        // Trim list if too long
        if (logs.Count > maxLogCount)
        {
            logs.RemoveAt(0);
        }

        // Auto-scroll to bottom
        scrollPosition.y = float.MaxValue;
    }

    void OnGUI()
    {
        if (!showConsole) return;

        if (!stylesInitialized) InitializeStyles();

        // Draw a semi-transparent background box covering the top half of screen
        float height = Screen.height * 0.5f;
        GUI.Box(new Rect(0, 0, Screen.width, height), "Debug Console (~ to close)");

        // Begin Scroll View
        GUILayout.BeginArea(new Rect(10, 20, Screen.width - 20, height - 30));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Draw Logs
        foreach (Log log in logs)
        {
            GUIStyle style = logStyle;
            if (log.type == LogType.Warning) style = warningStyle;
            else if (log.type == LogType.Error || log.type == LogType.Exception) style = errorStyle;

            GUILayout.Label(log.message, style);
            
            // Show stack trace only for errors/exceptions to save space
            if (log.type == LogType.Exception || log.type == LogType.Error)
            {
                GUILayout.Label("   " + log.stackTrace, style);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void InitializeStyles()
    {
        // Create styles with readable font sizes and colors
        
        logStyle = new GUIStyle(GUI.skin.label);
        logStyle.normal.textColor = Color.white;
        logStyle.fontSize = 14;

        warningStyle = new GUIStyle(GUI.skin.label);
        warningStyle.normal.textColor = Color.yellow;
        warningStyle.fontSize = 14;

        errorStyle = new GUIStyle(GUI.skin.label);
        errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f); // Light Red
        errorStyle.fontSize = 14;
        errorStyle.fontStyle = FontStyle.Bold;

        stylesInitialized = true;
    }
}