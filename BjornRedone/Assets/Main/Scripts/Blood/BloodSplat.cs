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
    [SerializeField] private float lifetime = 15f;
    [SerializeField] private bool randomizeRotation = true;

    private SpriteRenderer sr;
    private Vector2 velocity;
    private bool isMoving = true;
    private Vector3 targetScale;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        // Removed VisibleInsideMask to simplify setup. 
        // Now blood just draws normally. Ensure its Sorting Order is higher than the floor (e.g. -50 vs -100).
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
        // Assuming Floor is -100 to -10. We use -50 to -40 range.
        sr.sortingOrder = -3000 + Random.Range(0, 10);

        StartCoroutine(FadeAndDestroy());
    }

    private void Update()
    {
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

    private IEnumerator FadeAndDestroy()
    {
        yield return new WaitForSeconds(lifetime * 0.7f); // Wait 70% of lifetime
        
        float fadeDuration = lifetime * 0.3f;
        float timer = 0f;
        Color startColor = sr.color;

        while (timer < fadeDuration)
        {
            float alpha = Mathf.Lerp(startColor.a, 0f, timer / fadeDuration);
            sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }
        
        Destroy(gameObject);
    }
}