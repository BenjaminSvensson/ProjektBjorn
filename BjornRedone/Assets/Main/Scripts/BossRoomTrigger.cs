using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BossRoomTrigger : MonoBehaviour
{
    [Header("Music Settings")]
    public AudioClip bossMusic;

    [Header("Boss Settings")]
    [Tooltip("Drag your Penguin Boss here to wake him up.")]
    public GameObject bossGameObject;

    private bool hasTriggered = false;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            StartBossFight();
        }
    }

    void StartBossFight()
    {
        // 1. FIND THE MUSIC OBJECT AUTOMATICALLY
        // We look for the script "Music_slider" that you mentioned exists on the music object.
        Music_slider musicScript = FindObjectOfType<Music_slider>();

        if (musicScript != null)
        {
            // Now get the AudioSource attached to that same object
            AudioSource source = musicScript.GetComponent<AudioSource>();

            if (source != null)
            {
                source.Stop();
                source.clip = bossMusic;
                source.loop = true;
                source.Play();
            }
            else
            {
                Debug.LogError("Found object with 'Music_slider', but it has no AudioSource!");
            }
        }
        else
        {
            Debug.LogError("Could not find the Music Object! Is the 'Music_slider' script attached to it?");
        }

        // 2. ACTIVATE BOSS
        if (bossGameObject != null)
        {
            bossGameObject.SetActive(true);
        }
    }
}