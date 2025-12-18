using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform playerTransform;

    [Header("Motion Settings")]
    [Tooltip("Time it takes to reach the target. Lower = Snappier, Higher = Smoother.")]
    [SerializeField] private float smoothTime = 0.15f;
    
    [Header("Peek Settings")]
    [Tooltip("How much the mouse position pulls the camera (0 to 1).")]
    [Range(0f, 1f)] 
    [SerializeField] private float mouseInfluence = 0.3f;
    
    [Tooltip("The maximum distance the camera can shift away from the player.")]
    [SerializeField] private float maxPeekDistance = 6f;

    [Header("Dynamic Wall Locking")]
    [Tooltip("CRITICAL: Set this to the layer your Walls are on.")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("How far to check for walls in X and Y directions. Should be larger than your largest room size.")]
    [SerializeField] private Vector2 wallScanDistance = new Vector2(25f, 15f);
    [Tooltip("How much extra space to allow beyond the wall (visual padding). Prevents harsh snaps.")]
    [SerializeField] private float roomBuffer = 0.5f;
    
    private Vector3 currentVelocity;
    private Camera cam;
    private float defaultZ;

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultZ = transform.position.z;
    }

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (obstacleLayer.value == 0)
        {
            Debug.LogWarning("CAMERA WARNING: 'Obstacle Layer' is set to Nothing! The camera will go through walls.");
        }
    }

    void LateUpdate()
    {
        if (playerTransform == null) return;

        // 1. Calculate Base Target (Player + Peek)
        Vector3 baseTarget = playerTransform.position;
        Vector3 peekOffset = Vector3.zero;

        if (Mouse.current != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, -defaultZ));
            Vector3 dirToMouse = mouseWorldPos - playerTransform.position;
            
            peekOffset = Vector3.ClampMagnitude(dirToMouse * mouseInfluence, maxPeekDistance);
        }

        Vector3 desiredPosition = baseTarget + peekOffset;

        // 2. Dynamic Wall Locking
        if (obstacleLayer.value != 0)
        {
            float camHeight = cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;

            // Initialize bounds to infinity (Open world)
            float minX = float.MinValue;
            float maxX = float.MaxValue;
            float minY = float.MinValue;
            float maxY = float.MaxValue;

            // We use RaycastAll to ensure we can ignore the player's own collider/triggers
            // RIGHT
            float rightWallX = GetWallPosition(Vector2.right, wallScanDistance.x);
            if (rightWallX != float.MaxValue) maxX = rightWallX - camWidth + roomBuffer;

            // LEFT
            float leftWallX = GetWallPosition(Vector2.left, wallScanDistance.x);
            if (leftWallX != float.MaxValue) minX = leftWallX + camWidth - roomBuffer;

            // UP
            float topWallY = GetWallPosition(Vector2.up, wallScanDistance.y);
            if (topWallY != float.MaxValue) maxY = topWallY - camHeight + roomBuffer;

            // DOWN
            float bottomWallY = GetWallPosition(Vector2.down, wallScanDistance.y);
            if (bottomWallY != float.MaxValue) minY = bottomWallY + camHeight - roomBuffer;

            // 3. Clamp
            // Handle edge case where room is smaller than camera -> Center it
            if (minX > maxX) desiredPosition.x = (minX + maxX) / 2f;
            else desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);

            if (minY > maxY) desiredPosition.y = (minY + maxY) / 2f;
            else desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
            
            // Debug Visualization
            Debug.DrawLine(new Vector3(minX - camWidth, playerTransform.position.y, 0), new Vector3(maxX + camWidth, playerTransform.position.y, 0), Color.red);
            Debug.DrawLine(new Vector3(playerTransform.position.x, minY - camHeight, 0), new Vector3(playerTransform.position.x, maxY + camHeight, 0), Color.blue);
        }

        // 4. Apply Movement
        desiredPosition.z = defaultZ;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
    }

    private float GetWallPosition(Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(playerTransform.position, direction, distance, obstacleLayer);
        
        float closestHit = float.MaxValue;
        bool found = false;

        foreach (var hit in hits)
        {
            // Ignore the player themselves and any triggers (like room zones)
            if (hit.collider.transform != playerTransform && !hit.collider.isTrigger)
            {
                // Find the closest valid wall
                if (hit.distance < Mathf.Abs(closestHit)) // Comparison logic depends on direction, simplified here using distance
                {
                    // For Raycast, hit.point is correct
                    if (direction.x != 0) closestHit = hit.point.x;
                    else closestHit = hit.point.y;
                    
                    found = true;
                    // Since RaycastAll isn't sorted, we must check all, but usually we want the FIRST solid thing.
                    // Physics2D.Raycast typically returns closest, RaycastAll order is undefined.
                    // Let's rely on finding the one closest to player.
                }
            }
        }
        
        // Re-run to strictly find closest based on distance to player
        if (found)
        {
            float minDist = float.MaxValue;
            float finalPos = float.MaxValue;
            
            foreach (var hit in hits)
            {
                if (hit.collider.transform != playerTransform && !hit.collider.isTrigger)
                {
                    if (hit.distance < minDist)
                    {
                        minDist = hit.distance;
                        if (direction.x != 0) finalPos = hit.point.x;
                        else finalPos = hit.point.y;
                    }
                }
            }
            return finalPos;
        }

        return float.MaxValue;
    }
}