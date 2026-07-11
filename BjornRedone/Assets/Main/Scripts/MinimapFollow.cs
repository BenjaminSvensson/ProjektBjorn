using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Drag your Player object here")]
    public Transform playerTarget;

    [Header("Camera Settings")]
    [Tooltip("If true, the camera rotates with the player. If false, North remains Up.")]
    public bool rotateWithPlayer = false;

    // We store the camera's initial Z position so we don't clip into the 2D plane
    private float initialZPosition;
    private Camera mapCamera;
    private float followOrthographicSize;
    private bool showingOverview;

    void Start()
    {
        // Cache the initial Z position of the camera (usually -10 in 2D)
        initialZPosition = transform.position.z;
        mapCamera = GetComponent<Camera>();
        if (mapCamera != null) followOrthographicSize = mapCamera.orthographicSize;
    }

    // We use LateUpdate to ensure the player has finished moving before the camera follows
    // This prevents jitter
    void LateUpdate()
    {
        if (playerTarget == null || showingOverview) return;

        // 1. Follow Position
        // We move X and Y to match player, but keep Z static
        Vector3 newPosition = playerTarget.position;
        newPosition.z = initialZPosition; 
        transform.position = newPosition;

        // 2. Handle Rotation
        if (rotateWithPlayer)
        {
            // Sync rotation with player (e.g., for driving games)
            transform.rotation = Quaternion.Euler(0, 0, playerTarget.eulerAngles.z);
        }
        else
        {
            // Keep rotation locked (Standard Minimap)
            transform.rotation = Quaternion.identity;
        }
    }

    public void ShowOverview(Bounds worldBounds)
    {
        showingOverview = true;
        transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, initialZPosition);
        transform.rotation = Quaternion.identity;

        if (mapCamera != null && mapCamera.orthographic)
        {
            float halfWidth = worldBounds.extents.x + 2.5f;
            float halfHeight = worldBounds.extents.y + 2.5f;
            float aspect = Mathf.Max(0.01f, mapCamera.aspect);
            mapCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);
        }
    }

    public void ShowPlayerFollow()
    {
        showingOverview = false;
        if (mapCamera != null && mapCamera.orthographic) mapCamera.orthographicSize = followOrthographicSize;
    }
}
