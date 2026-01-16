using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class IceWall : MonoBehaviour
{
    public float fadeDuration = 0.5f; 
    private SpriteRenderer sr;

    public void Activate(float totalLifeTime)
    {
        sr = GetComponent<SpriteRenderer>();
        
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

        // Wait
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
        if (sr) { Color c = sr.color; c.a = alpha; sr.color = c; }
    }
}