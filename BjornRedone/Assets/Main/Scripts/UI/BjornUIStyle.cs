using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared runtime shapes for Bjorn's minimal black and white UI.</summary>
public static class BjornUIStyle
{
    private static Sprite roundedSprite;
    private static Sprite circleSprite;
    private static Sprite pillSprite;

    public static void ApplyRounded(Image image)
    {
        if (image == null) return;
        if (roundedSprite == null) roundedSprite = CreateRoundedSprite(64, 15f, 16f);
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
    }

    public static void ApplyCircle(Image image)
    {
        if (image == null) return;
        if (circleSprite == null) circleSprite = CreateRoundedSprite(64, 31f, 24f);
        image.sprite = circleSprite;
        image.type = Image.Type.Simple;
    }

    public static void ApplyPill(Image image)
    {
        if (image == null) return;
        if (pillSprite == null) pillSprite = CreateRoundedSprite(32, 15f, 4f);
        image.sprite = pillSprite;
        image.type = Image.Type.Sliced;
    }

    private static Sprite CreateRoundedSprite(int size, float radius, float border)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Bjorn UI Shape",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1f) * 0.5f;
        float half = center;
        float inner = half - radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x - center) - inner;
                float qy = Mathf.Abs(y - center) - inner;
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                float distance = outside + inside - radius;
                float alpha = Mathf.Clamp01(0.75f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
