using UnityEngine;

/// <summary>
/// A smooth, room-based camera system with Screen Shake support.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RoomCamera : MonoBehaviour
{
    public static RoomCamera Instance { get; private set; } // --- NEW: Singleton ---

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
    [SerializeField] private float followStrength = 0.15f; 

    [Tooltip("If true, the camera stops moving at the room edges.")]
    [SerializeField] private bool clampToRoom = true;

    [Header("Zoom Control")]
    [Tooltip("If checked, the script will change Orthographic Size so the camera fits inside the room.")]
    [SerializeField] private bool autoFitToRoom = true;
    
    [Tooltip("Multiplier for the auto-fit. 1.0 = Exact Fit. < 1.0 = Zoomed In (allows panning). > 1.0 = Zoomed Out.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float zoomScale = 0.9f; 

    // --- Private State ---
    private Vector3 currentVelocity; 
    private Camera cam;
    
    // --- Shake State ---
    private float shakeTimer = 0f;
    private float shakeIntensity = 0f;
    private Vector3 shakeOffset = Vector3.zero;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        cam = GetComponent<Camera>();

        // Ensure background is solid color
        cam.clearFlags = CameraClearFlags.SolidColor;
        
        if (ColorUtility.TryParseHtmlString("#5DB64A", out Color bgColor))
        {
            cam.backgroundColor = bgColor;
        }
        else
        {
            cam.backgroundColor = Color.black; 
        }

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

        if (target != null)
        {
            transform.position = CalculateTargetPosition(target.position);
        }

        if (autoFitToRoom)
        {
            FitCameraToRoom();
        }
    }

    // --- NEW: Shake Method ---
    public void Shake(float duration, float magnitude)
    {
        shakeTimer = duration;
        shakeIntensity = magnitude;
    }

    void LateUpdate()
    {
        if (autoFitToRoom)
        {
            FitCameraToRoom();
        }

        if (target == null) return;

        // 1. Calculate Base Target
        Vector3 desiredPosition = CalculateTargetPosition(target.position);

        // 2. Smoothly move towards that base position
        Vector3 smoothedPos = Vector3.SmoothDamp(
            transform.position - shakeOffset, // Damp from the "unshaken" position
            desiredPosition, 
            ref currentVelocity, 
            smoothTime
        );

        // 3. Calculate Shake Offset
        if (shakeTimer > 0)
        {
            shakeOffset = Random.insideUnitSphere * shakeIntensity;
            // Keep shake on 2D plane
            shakeOffset.z = 0; 
            shakeTimer -= Time.deltaTime;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        // 4. Apply Final Position
        transform.position = smoothedPos + shakeOffset;
    }

    private Vector3 CalculateTargetPosition(Vector3 playerPos)
    {
        Vector3 roomCenter = GetGridCenter(playerPos);
        Vector3 rawOffset = playerPos - roomCenter;
        Vector3 followOffset = rawOffset * followStrength;
        Vector3 finalTarget = roomCenter + followOffset;
        finalTarget.z = fixedZ;

        if (clampToRoom)
        {
            float camHeight = cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;

            float maxDx = (roomSize.x / 2f) - camWidth;
            float maxDy = (roomSize.y / 2f) - camHeight;

            float clampedX = (maxDx > 0) ? Mathf.Clamp(finalTarget.x - roomCenter.x, -maxDx, maxDx) : 0f;
            float clampedY = (maxDy > 0) ? Mathf.Clamp(finalTarget.y - roomCenter.y, -maxDy, maxDy) : 0f;

            finalTarget.x = roomCenter.x + clampedX;
            finalTarget.y = roomCenter.y + clampedY;
        }

        return finalTarget;
    }

    private void FitCameraToRoom()
    {
        if (!cam.orthographic) return;

        float effectiveHeight = roomSize.y * zoomScale;
        float effectiveWidth = roomSize.x * zoomScale;

        float sizeForHeight = effectiveHeight / 2f;
        float resultingWidth = sizeForHeight * cam.aspect * 2f;

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

    private Vector3 GetGridCenter(Vector3 position)
    {
        int gridX = Mathf.RoundToInt(position.x / roomSize.x);
        int gridY = Mathf.RoundToInt(position.y / roomSize.y);

        float centerX = gridX * roomSize.x;
        float centerY = gridY * roomSize.y;

        return new Vector3(centerX, centerY, 0); 
    }

    void OnDrawGizmos()
    {
        if (cam == null) cam = GetComponent<Camera>();
        
        Gizmos.color = Color.green;
        Vector3 currentGridCenter = GetGridCenter(transform.position);
        Gizmos.DrawWireCube(currentGridCenter, new Vector3(roomSize.x, roomSize.y, 1));

        if (cam != null)
        {
            Gizmos.color = Color.yellow;
            float camHeight = cam.orthographicSize * 2;
            float camWidth = camHeight * cam.aspect;
            Gizmos.DrawWireCube(transform.position, new Vector3(camWidth, camHeight, 1));
        }
    }
}