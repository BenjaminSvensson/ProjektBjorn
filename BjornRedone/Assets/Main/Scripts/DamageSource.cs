using UnityEngine;
using System.Collections; 
using System.Collections.Generic; 

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))] 
public class DamageSource : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("The amount of damage to deal.")]
    [SerializeField] private float damageAmount = 10f;

    [Tooltip("The tag of the object to damage. Should be 'Player'.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Damage Type")]
    [Tooltip("Is this damage over time (like poison)? If false, damage is applied once.")]
    [SerializeField] private bool isTickDamage = false;

    [Tooltip("How often to apply tick damage (in seconds). Only used if 'Is Tick Damage' is true.")]
    [SerializeField] private float tickRate = 1f;

    [Header("Beartrap Settings")]
    [Tooltip("If checked, this trap will stop the player from moving for a duration.")]
    [SerializeField] private bool isBeartrap = false;
    [Tooltip("How long (in seconds) the player is stuck. Only used if 'Is Beartrap' is true.")]
    [SerializeField] private float trapDuration = 3f;
    // --- NEW: Beartrap Visuals ---
    [Tooltip("The GameObject holding the 'open' sprite. Will be hidden when triggered.")]
    [SerializeField] private GameObject openVisual;
    [Tooltip("The GameObject holding the 'closed' sprite. Will be shown when triggered.")]
    [SerializeField] private GameObject closedVisual;
    // --- END NEW ---

    [Header("Destruction")]
    [Tooltip("Should this object be destroyed after its first damage impact? (e.g., a bullet, a beartrap)")]
    [SerializeField] private bool destroyOnImpact = false;
    [Tooltip("How long to fade out the sprite before destroying.")]
    [SerializeField] private float fadeOutTime = 0.5f;

    [Header("Audio")]
    [Tooltip("The sound to play when damage is dealt.")]
    [SerializeField] private AudioClip damageSound;
    private AudioSource audioSource;

    private Coroutine tickDamageCoroutine;
    private PlayerLimbController currentlyTickingPlayer;
    private bool isTriggered = false; 
    private List<SpriteRenderer> allRenderers = new List<SpriteRenderer>(); 

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        
        GetComponentsInChildren<SpriteRenderer>(allRenderers);

        if (isBeartrap)
        {
            if (openVisual != null) openVisual.SetActive(true);
            if (closedVisual != null) closedVisual.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        // --- Check for Player ---
        if (other.CompareTag(playerTag))
        {
            PlayerLimbController player = other.GetComponent<PlayerLimbController>();
            
            if (player != null)
            {
                Vector2 hitDir = (other.transform.position - transform.position).normalized;

                if (isBeartrap)
                {
                    isTriggered = true; 
                    PlayerMovement playerMovement = other.GetComponent<PlayerMovement>();
                    
                    if (playerMovement != null)
                    {
                        Debug.Log("BEARTRAP! Player is trapped.");
                        playerMovement.SetTrapped(true);
                    }
                    
                    // --- PASS DIRECTION ---
                    player.TakeDamage(damageAmount, hitDir);
                    PlayDamageSound();
                    
                    if (openVisual != null) openVisual.SetActive(false);
                    if (closedVisual != null) closedVisual.SetActive(true);

                    StartCoroutine(BeartrapReleaseCoroutine(playerMovement));
                    return; 
                }
                
                if (isTickDamage)
                {
                    if (tickDamageCoroutine == null)
                    {
                        currentlyTickingPlayer = player;
                        tickDamageCoroutine = StartCoroutine(TickDamage(player));
                    }
                }
                else
                {
                    player.TakeDamage(damageAmount, hitDir);

                    if (destroyOnImpact)
                    {
                        isTriggered = true; 
                        StartCoroutine(PlaySoundAndDestroy());
                    }
                    else
                    {
                        PlayDamageSound(); 
                    }
                }
            }
        }
        // --- Check for Enemy ---
        else if (other.GetComponent<EnemyLimbController>() != null)
        {
            EnemyLimbController enemy = other.GetComponent<EnemyLimbController>();
            Vector2 hitDir = (other.transform.position - transform.position).normalized;
            
            if (isBeartrap)
            {
                isTriggered = true; 
                EnemyAI enemyAI = other.GetComponent<EnemyAI>();
                
                if (enemyAI != null)
                {
                    Debug.Log("BEARTRAP! Enemy is trapped.");
                    enemyAI.SetTrapped(true);
                }
                
                enemy.TakeDamage(damageAmount, hitDir);
                PlayDamageSound();
                
                if (openVisual != null) openVisual.SetActive(false);
                if (closedVisual != null) closedVisual.SetActive(true);

                StartCoroutine(BeartrapReleaseEnemyCoroutine(enemyAI));
                return;
            }
            
            enemy.TakeDamage(damageAmount, hitDir);

            if (destroyOnImpact)
            {
                isTriggered = true; 
                StartCoroutine(PlaySoundAndDestroy());
            }
            else
            {
                PlayDamageSound();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (isTickDamage && other.CompareTag(playerTag))
        {
            PlayerLimbController player = other.GetComponent<PlayerLimbController>();
            
            if (player != null && player == currentlyTickingPlayer)
            {
                if (tickDamageCoroutine != null)
                {
                    Debug.Log("Stopping tick damage...");
                    StopCoroutine(tickDamageCoroutine);
                    tickDamageCoroutine = null;
                    currentlyTickingPlayer = null;
                }
            }
        }
    }

    private IEnumerator TickDamage(PlayerLimbController player)
    {
        Debug.Log("Starting tick damage...");
        while (true)
        {
            if (player == null)
            {
                Debug.Log("Player is null, stopping tick damage.");
                tickDamageCoroutine = null;
                yield break; 
            }
            
            // NOTE: Tick damage doesn't have a distinct "direction" every frame usually, 
            // but we can pass Vector2.zero or calculate fresh each time.
            Vector2 hitDir = (player.transform.position - transform.position).normalized;
            player.TakeDamage(damageAmount, hitDir);
            PlayDamageSound();
            
            yield return new WaitForSeconds(tickRate);
        }
    }

    private IEnumerator PlaySoundAndDestroy()
    {
        GetComponent<Collider2D>().enabled = false;
        
        PlayDamageSound();
        
        float soundLength = damageSound != null ? damageSound.length : 0f;
        float waitTime = Mathf.Max(soundLength, fadeOutTime);

        yield return StartCoroutine(FadeOut(fadeOutTime));
        
        if (waitTime > fadeOutTime)
        {
            yield return new WaitForSeconds(waitTime - fadeOutTime);
        }

        Destroy(gameObject);
    }
    
    private IEnumerator BeartrapReleaseCoroutine(PlayerMovement playerMovement)
    {
        GetComponent<Collider2D>().enabled = false;

        yield return new WaitForSeconds(trapDuration);
        
        if (playerMovement != null)
        {
            Debug.Log("Player released from trap.");
            playerMovement.SetTrapped(false);
        }
        
        yield return StartCoroutine(FadeOut(fadeOutTime));
        Destroy(gameObject);
    }

    private IEnumerator BeartrapReleaseEnemyCoroutine(EnemyAI enemyAI)
    {
        GetComponent<Collider2D>().enabled = false;

        yield return new WaitForSeconds(trapDuration);
        
        if (enemyAI != null)
        {
            Debug.Log("Enemy released from trap.");
            enemyAI.SetTrapped(false);
        }
        
        yield return StartCoroutine(FadeOut(fadeOutTime));
        Destroy(gameObject);
    }

    private IEnumerator FadeOut(float duration)
    {
        float timer = 0f;
        
        allRenderers.Clear();
        GetComponentsInChildren<SpriteRenderer>(allRenderers);

        Dictionary<SpriteRenderer, Color> initialColors = new Dictionary<SpriteRenderer, Color>();
        foreach (var sr in allRenderers)
        {
            if(sr != null) initialColors[sr] = sr.color;
        }

        while (timer < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / duration);
            foreach (var entry in initialColors)
            {
                if(entry.Key != null)
                    entry.Key.color = new Color(entry.Value.r, entry.Value.g, entry.Value.b, alpha);
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
        
        foreach (var entry in initialColors)
        {
            if(entry.Key != null)
                entry.Key.color = new Color(entry.Value.r, entry.Value.g, entry.Value.b, 0f);
        }
    }

    private void PlayDamageSound()
    {
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
    }
}