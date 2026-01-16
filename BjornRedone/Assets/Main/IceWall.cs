using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class IceWall : MonoBehaviour
{
    [Header("Visual Settings")]
    public float fadeDuration = 0.5f; 

    private SpriteRenderer sr;
    private Collider2D col;

    public void Activate(float totalLifeTime)
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        
        // Start invisible
        Color c = sr.color;
        c.a = 0f;
        sr.color = c;

        StartCoroutine(FadeRoutine(totalLifeTime));
    }

    private IEnumerator FadeRoutine(float lifeTime)
    {
        // Fade In
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / fadeDuration;
            SetAlpha(Mathf.Lerp(0f, 1f, t));
            yield return null;
        }
        SetAlpha(1f);

        // Wait (Total time - time spent fading in and out)
        float waitTime = lifeTime - (fadeDuration * 2);
        if (waitTime > 0) yield return new WaitForSeconds(waitTime);

        // Fade Out
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / fadeDuration;
            SetAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }
        SetAlpha(0f);

        Destroy(gameObject);
    }

    void SetAlpha(float alpha)
    {
        if (sr)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}