using UnityEngine;

public class WarningFader : MonoBehaviour
{
    [Header("Settings")]
    public float fadeSpeed = 5f;
    public float lifeTime = 2.0f; // Should match or exceed the warning delay

    private SpriteRenderer sr;
    private Color originalColor;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr) originalColor = sr.color;
        
        // Auto destroy itself after the job is done to keep hierarchy clean
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (sr == null) return;

        // Math logic to make it pulse/fade in and out
        float alpha = Mathf.Abs(Mathf.Sin(Time.time * fadeSpeed));
        sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
    }
}