using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class BloodSplat : MonoBehaviour
{
    [Header("Fluid Physics")]
    [SerializeField] private float deceleration = 5f;
    [SerializeField] private float spreadSpeed = 5f;
    [SerializeField] private float maxScale = 1f;
    
    [Header("Settings")]
    // Removed 'lifetime' variable as it is no longer needed
    [SerializeField] private bool randomizeRotation = true;

    private SpriteRenderer sr;
    private Vector2 velocity;
    private bool isMoving = true;
    private Vector3 targetScale;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        // Ensure its Sorting Order is higher than the floor (e.g. -50 vs -100).
        sr.maskInteraction = SpriteMaskInteraction.None;
    }

    public void Initialize(Vector2 startVelocity, float sizeMultiplier)
    {
        velocity = startVelocity;
        
        // Randomize the final size slightly for variety
        float finalSize = maxScale * sizeMultiplier * Random.Range(0.8f, 1.2f);
        targetScale = new Vector3(finalSize, finalSize, 1f);
        
        // Start tiny (droplet)
        transform.localScale = Vector3.zero;

        // Random rotation
        if (randomizeRotation)
        {
            transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        }

        // Layering: Randomize sorting order slightly so droplets stack naturally
        // Assuming Floor is -100 to -10. We use -32767 range roughly.
        sr.sortingOrder = -32767 + Random.Range(0, 100);

        // REMOVED: StartCoroutine(FadeAndDestroy());
    }

    private void Update()
    {
        // Once physics are done, this script stops running logic, 
        // effectively making the object a static prop.
        if (!isMoving) return;

        // 1. Move "Physics"
        transform.Translate(velocity * Time.deltaTime, Space.World);
        
        // 2. Decelerate (Friction)
        velocity = Vector2.Lerp(velocity, Vector2.zero, deceleration * Time.deltaTime);

        // 3. Spread (Scale Up) - Mimics liquid hitting surface
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, spreadSpeed * Time.deltaTime);

        // Stop calculating when slow enough and big enough
        if (velocity.magnitude < 0.1f && transform.localScale.x > targetScale.x * 0.95f)
        {
            isMoving = false;
        }
    }

    // REMOVED: FadeAndDestroy() coroutine
}