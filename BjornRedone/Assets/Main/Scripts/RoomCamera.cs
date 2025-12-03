using UnityEngine;

/// <summary>
/// A smooth, room-based camera system.
/// It detects which "grid cell" the player is in and pans the camera to center on it.
/// It also auto-zooms to ensure the camera never peeks outside the room walls.
/// Attach this to your Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RoomCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Player's Transform.")]
    [SerializeField] private Transform target;

    [Header("Settings")]
    [Tooltip("MUST match the 'Room Size' in your LevelGenerator.")]
    [SerializeField] private Vector2 roomSize = new Vector2(20, 10);
    
    [Tooltip("How long it takes the camera to move to the new room (in seconds).")]
    [SerializeField] private float smoothTime = 0.25f;
    
    [Tooltip("The Z position for the camera (usually -10).")]
    [SerializeField] private float fixedZ = -10f;

    [Header("Zoom Control")]
    [Tooltip("If checked, the script will change Orthographic Size so the camera perfectly fits inside the room walls.")]
    [SerializeField] private bool autoFitToRoom = true;

    // --- Private State ---
    private Vector3 currentVelocity; // Used by SmoothDamp
    private Vector3 targetPos;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();

        // If the player wasn't assigned, try to find them
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogError("RoomCamera could not find a target (Player)!");
            }
        }

        // Snap immediately to the start room
        if (target != null)
        {
            targetPos = GetGridCenter(target.position);
            transform.position = targetPos;
        }

        if (autoFitToRoom)
        {
            FitCameraToRoom();
        }
    }

    void LateUpdate()
    {
        // 1. Handle Zoom (in case screen resolution changes)
        if (autoFitToRoom)
        {
            FitCameraToRoom();
        }

        if (target == null) return;

        // 2. Calculate the center of the room the player is currently in
        Vector3 desiredPosition = GetGridCenter(target.position);

        // 3. Smoothly move the camera towards that position
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref currentVelocity, 
            smoothTime
        );
    }

    /// <summary>
    /// Adjusts the Camera's Orthographic Size so the view is strictly 
    /// contained within the roomSize dimensions.
    /// </summary>
    private void FitCameraToRoom()
    {
        if (!cam.orthographic)
        {
            Debug.LogWarning("RoomCamera: Auto Fit requires an Orthographic camera!");
            return;
        }

        // 1. Calculate the Size needed to fit the Height perfectly
        float sizeForHeight = roomSize.y / 2f;

        // 2. Calculate what the Width would be at that size
        float resultingWidth = sizeForHeight * cam.aspect * 2f;

        // 3. If that width is too big for our room, we must shrink the camera based on Width instead
        if (resultingWidth > roomSize.x)
        {
            // Calculate size to fit Width perfectly
            float sizeForWidth = (roomSize.x / cam.aspect) / 2f;
            cam.orthographicSize = sizeForWidth;
        }
        else
        {
            // Otherwise, fitting to Height is safe
            cam.orthographicSize = sizeForHeight;
        }
    }

    /// <summary>
    /// Converts a world position (like the player's) into the center position 
    /// of the nearest room grid cell.
    /// </summary>
    private Vector3 GetGridCenter(Vector3 position)
    {
        // We round the position divided by room size to find the integer grid coordinate
        int gridX = Mathf.RoundToInt(position.x / roomSize.x);
        int gridY = Mathf.RoundToInt(position.y / roomSize.y);

        // Multiply back to get the world center of that grid cell
        float centerX = gridX * roomSize.x;
        float centerY = gridY * roomSize.y;

        return new Vector3(centerX, centerY, fixedZ);
    }
}