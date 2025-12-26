using UnityEngine;
using System.Collections; 
using System.Collections.Generic; 

[RequireComponent(typeof(Collider2D), typeof(AudioSource))]
public class DamageSource : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool isTickDamage = false;
    [SerializeField] private float tickRate = 1f;
    [SerializeField] private bool isBeartrap = false;
    [SerializeField] private float trapDuration = 3f;
    [SerializeField] private GameObject openVisual, closedVisual;
    [SerializeField] private bool destroyOnImpact = false;
    [SerializeField] private float fadeOutTime = 0.5f;
    [SerializeField] private AudioClip damageSound;
    private AudioSource audioSource;
    private Coroutine tickDamageCoroutine;
    private PlayerLimbController currentlyTickingPlayer;
    private bool isTriggered = false; 
    private List<SpriteRenderer> allRenderers = new List<SpriteRenderer>(); 

    void Awake()
    {
        audioSource = GetComponent<AudioSource>(); audioSource.playOnAwake = false;
        GetComponentsInChildren<SpriteRenderer>(allRenderers);
        if (isBeartrap) { openVisual?.SetActive(true); closedVisual?.SetActive(false); }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        // --- NEW: Weapon Logic ---
        // If a weapon is thrown into the trap
        WeaponPickup weapon = other.GetComponent<WeaponPickup>();
        if (weapon != null && !weapon.CanPickup()) // !CanPickup implies isFlying
        {
            if (isBeartrap)
            {
                isTriggered = true;
                weapon.GetStuck();
                weapon.transform.SetParent(transform); // Attach to trap
                
                TriggerTrapVisuals();
                PlayDamageSound();
                
                StartCoroutine(TrapDestroyRoutine()); // Destroy trap + weapon
                return;
            }
        }
        // -------------------------

        if (other.CompareTag(playerTag))
        {
            PlayerLimbController player = other.GetComponent<PlayerLimbController>();
            if (player != null)
            {
                Vector2 hitDir = (other.transform.position - transform.position).normalized;
                if (isBeartrap) { isTriggered = true; other.GetComponent<PlayerMovement>()?.SetTrapped(true); player.TakeDamage(damageAmount, hitDir); PlayDamageSound(); TriggerTrapVisuals(); StartCoroutine(BeartrapReleaseCoroutine(other.GetComponent<PlayerMovement>())); return; }
                if (isTickDamage) { if (tickDamageCoroutine == null) { currentlyTickingPlayer = player; tickDamageCoroutine = StartCoroutine(TickDamage(player)); } }
                else { player.TakeDamage(damageAmount, hitDir); if (destroyOnImpact) { isTriggered = true; StartCoroutine(PlaySoundAndDestroy()); } else PlayDamageSound(); }
            }
        }
        else if (other.GetComponent<EnemyLimbController>())
        {
            EnemyLimbController enemy = other.GetComponent<EnemyLimbController>();
            Vector2 hitDir = (other.transform.position - transform.position).normalized;
            if (isBeartrap) { isTriggered = true; other.GetComponent<EnemyAI>()?.SetTrapped(true); enemy.TakeDamage(damageAmount, hitDir); PlayDamageSound(); TriggerTrapVisuals(); StartCoroutine(BeartrapReleaseEnemyCoroutine(other.GetComponent<EnemyAI>())); return; }
            enemy.TakeDamage(damageAmount, hitDir); if (destroyOnImpact) { isTriggered = true; StartCoroutine(PlaySoundAndDestroy()); } else PlayDamageSound();
        }
    }

    private void OnTriggerExit2D(Collider2D other) { if (isTickDamage && other.CompareTag(playerTag) && other.GetComponent<PlayerLimbController>() == currentlyTickingPlayer && tickDamageCoroutine != null) { StopCoroutine(tickDamageCoroutine); tickDamageCoroutine = null; currentlyTickingPlayer = null; } }

    private IEnumerator TickDamage(PlayerLimbController p) { while (p != null) { p.TakeDamage(damageAmount, (p.transform.position - transform.position).normalized); PlayDamageSound(); yield return new WaitForSeconds(tickRate); } tickDamageCoroutine = null; }

    private void TriggerTrapVisuals()
    {
        if (openVisual) openVisual.SetActive(false);
        if (closedVisual) closedVisual.SetActive(true);
    }

    private IEnumerator PlaySoundAndDestroy() { GetComponent<Collider2D>().enabled = false; PlayDamageSound(); yield return StartCoroutine(FadeOut(fadeOutTime)); Destroy(gameObject); }
    private IEnumerator BeartrapReleaseCoroutine(PlayerMovement m) { GetComponent<Collider2D>().enabled = false; yield return new WaitForSeconds(trapDuration); m?.SetTrapped(false); yield return StartCoroutine(FadeOut(fadeOutTime)); Destroy(gameObject); }
    private IEnumerator BeartrapReleaseEnemyCoroutine(EnemyAI a) { GetComponent<Collider2D>().enabled = false; yield return new WaitForSeconds(trapDuration); a?.SetTrapped(false); yield return StartCoroutine(FadeOut(fadeOutTime)); Destroy(gameObject); }
    
    // --- NEW: For Weapon-triggered traps ---
    private IEnumerator TrapDestroyRoutine()
    {
        GetComponent<Collider2D>().enabled = false;
        yield return StartCoroutine(FadeOut(fadeOutTime));
        Destroy(gameObject); // Destroys trap AND the child weapon
    }

    private IEnumerator FadeOut(float duration) {
        float timer = 0f; allRenderers.Clear(); GetComponentsInChildren<SpriteRenderer>(allRenderers);
        Dictionary<SpriteRenderer, Color> initials = new Dictionary<SpriteRenderer, Color>();
        foreach (var sr in allRenderers) if (sr) initials[sr] = sr.color;
        while (timer < duration) { float alpha = Mathf.Lerp(1f, 0f, timer / duration); foreach (var e in initials) if (e.Key) e.Key.color = new Color(e.Value.r, e.Value.g, e.Value.b, alpha); timer += Time.deltaTime; yield return null; }
    }

    private void PlayDamageSound() { if (audioSource && damageSound) audioSource.PlayOneShot(damageSound); }
}