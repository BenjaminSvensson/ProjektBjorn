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

    [Header("Grid Settings")]
    [Tooltip("MUST match the 'Room Size' in your LevelGenerator (e.g., X=20, Y=10).")]
    [SerializeField] private Vector2 roomSize = new Vector2(20, 10);
    
    [Tooltip("How long it takes the camera to move to the new room (in seconds).")]
    [SerializeField] private float smoothTime = 0.25f;
    
    [Tooltip("The Z position for the camera (usually -10).")]
    [SerializeField] private float fixedZ = -10f;

    [Header("Follow Settings")]
    [Tooltip("How much the camera leans towards the player within the room. 0 = Locked to Center, 1 = Locked to Player.")]
    [Range(0f, 1f)]
    [SerializeField] private float followStrength = 0.15f; // "Tiny bit" default

    [Tooltip("If true, the camera stops moving at the room edges.")]
    [SerializeField] private bool clampToRoom = true;

    [Header("Zoom Control")]
    [Tooltip("If checked, the script will change Orthographic Size so the camera fits inside the room.")]
    [SerializeField] private bool autoFitToRoom = true;
    
    [Tooltip("Multiplier for the auto-fit. 1.0 = Exact Fit. < 1.0 = Zoomed In (allows panning). > 1.0 = Zoomed Out.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float zoomScale = 0.9f; // Default slightly zoomed in to allow movement

    // --- Private State ---
    private Vector3 currentVelocity; // Used by SmoothDamp
    private Vector3 targetPos;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();

        // Ensure background is solid color
        cam.clearFlags = CameraClearFlags.SolidColor;
        
        // Set background color to #5DB64A (Greenish)
        if (ColorUtility.TryParseHtmlString("#5DB64A", out Color bgColor))
        {
            cam.backgroundColor = bgColor;
        }
        else
        {
            cam.backgroundColor = Color.black; // Fallback
        }

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
            transform.position = CalculateTargetPosition(target.position);
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

        // 2. Calculate desired position with follow logic
        Vector3 desiredPosition = CalculateTargetPosition(target.position);

        // 3. Smoothly move the camera towards that position
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref currentVelocity, 
            smoothTime
        );
    }

    private Vector3 CalculateTargetPosition(Vector3 playerPos)
    {
        // A. Find the center of the current room
        Vector3 roomCenter = GetGridCenter(playerPos);

        // B. Calculate offset based on player position within the room
        Vector3 rawOffset = playerPos - roomCenter;
        
        // C. Apply influence (The "Tiny Bit" follow)
        Vector3 followOffset = rawOffset * followStrength;

        // D. Construct Target
        Vector3 finalTarget = roomCenter + followOffset;
        finalTarget.z = fixedZ;

        // E. Clamp to room boundaries
        if (clampToRoom)
        {
            float camHeight = cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;

            // Calculate the maximum distance the camera can move from the center
            // before seeing outside the room
            float maxDx = (roomSize.x / 2f) - camWidth;
            float maxDy = (roomSize.y / 2f) - camHeight;

            // If maxDx is negative, the camera is wider than the room (can't clamp, just center X)
            // If positive, we can move that much.
            float clampedX = (maxDx > 0) ? Mathf.Clamp(finalTarget.x - roomCenter.x, -maxDx, maxDx) : 0f;
            float clampedY = (maxDy > 0) ? Mathf.Clamp(finalTarget.y - roomCenter.y, -maxDy, maxDy) : 0f;

            finalTarget.x = roomCenter.x + clampedX;
            finalTarget.y = roomCenter.y + clampedY;
        }

        return finalTarget;
    }

    /// <summary>
    /// Adjusts the Camera's Orthographic Size so the view is contained within the roomSize.
    /// Uses zoomScale to determine tightness.
    /// </summary>
    private void FitCameraToRoom()
    {
        if (!cam.orthographic)
        {
            Debug.LogWarning("RoomCamera: Auto Fit requires an Orthographic camera!");
            return;
        }

        // Apply the zoom scale to the room dimensions we are trying to fit
        // If zoomScale is 0.9, we pretend the room is smaller, so the camera zooms in.
        float effectiveHeight = roomSize.y * zoomScale;
        float effectiveWidth = roomSize.x * zoomScale;

        // 1. Calculate Size needed to fit Height
        float sizeForHeight = effectiveHeight / 2f;

        // 2. Calculate resulting Width
        float resultingWidth = sizeForHeight * cam.aspect * 2f;

        // 3. If Width is too big, shrink based on Width
        if (resultingWidth > effectiveWidth)
        {
            float sizeForWidth = (effectiveWidth / cam.aspect) / 2f;
            cam.orthographicSize = sizeForWidth;
        }
        else
        {
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

        return new Vector3(centerX, centerY, 0); // Z handled later
    }

    // Visual debugging
    void OnDrawGizmos()
    {
        if (cam == null) cam = GetComponent<Camera>();
        
        // Draw Room Grid Center
        Gizmos.color = Color.green;
        Vector3 currentGridCenter = GetGridCenter(transform.position);
        Gizmos.DrawWireCube(currentGridCenter, new Vector3(roomSize.x, roomSize.y, 1));

        // Draw Camera View
        if (cam != null)
        {
            Gizmos.color = Color.yellow;
            float camHeight = cam.orthographicSize * 2;
            float camWidth = camHeight * cam.aspect;
            Gizmos.DrawWireCube(transform.position, new Vector3(camWidth, camHeight, 1));
        }
    }
}