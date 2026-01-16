using UnityEngine;

public class RGB_randomizer : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    // Array of common flower colors
    private Color[] flowerColors = new Color[]
    {
        Color.red,
        Color.yellow,
        new Color(1f, 0.753f, 0.796f), // light pink
        Color.white,
        new Color(0.6f, 0.4f, 0.8f) // purple
    };

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // Pick a random color from the array
            Color randomColor = flowerColors[Random.Range(0, flowerColors.Length)];
            spriteRenderer.color = randomColor;
        }
        else
        {
            Debug.LogWarning("No SpriteRenderer found on " + gameObject.name);
        }
    }
}
