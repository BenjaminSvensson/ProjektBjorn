using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleCameraFollow : MonoBehaviour
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

    [Header("Fairness / Input Cap")]
    [Tooltip("The hard limit on how far the mouse is calculated from the player. Prevents large monitors from seeing further than small monitors.")]
    [SerializeField] private float maxMouseInputDistance = 12f;

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
    }

    void LateUpdate()
    {
        if (playerTransform == null) return;

        // 1. Calculate Base Target (Player Position)
        Vector3 targetPosition = playerTransform.position;
        Vector3 peekOffset = Vector3.zero;

        // 2. Calculate Peek Offset (Mouse Influence)
        if (Mouse.current != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            
            // Convert screen mouse position to world space
            // We use Abs(defaultZ) assuming the camera is at a negative Z looking at Z=0
            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(defaultZ)));
            
            Vector3 dirToMouse = mouseWorldPos - playerTransform.position;

            // --- FAIRNESS CAP ---
            // We clamp the input vector first. If the mouse is 50 units away (large screen), 
            // we treat it as if it's only 'maxMouseInputDistance' away.
            dirToMouse = Vector3.ClampMagnitude(dirToMouse, maxMouseInputDistance);

            // Apply influence
            peekOffset = dirToMouse * mouseInfluence;

            // Final safety clamp (usually handled by the input cap, but good for safety)
            peekOffset = Vector3.ClampMagnitude(peekOffset, maxPeekDistance);
        }

        Vector3 finalPosition = targetPosition + peekOffset;

        // 3. Apply Movement
        finalPosition.z = defaultZ; 
        
        transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref currentVelocity, smoothTime);
    }
}