using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class SceneMenuManager : MonoBehaviour
{
    [Header("Button Configuration")]
    [Tooltip("Add your buttons here and type the exact name of the scene they should load.")]
    public List<SceneLink> menuButtons;

    [Header("Satisfying Audio Settings")]
    public AudioSource uiAudioSource;
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Range(0f, 0.2f)]
    public float pitchRandomization = 0.1f; // Adds slight variation to make it feel organic

    [System.Serializable]
    public struct SceneLink
    {
        public Button button;
        public string sceneName;
    }

    void Start()
    {
        // Check if AudioSource is missing
        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
        }

        foreach (var link in menuButtons)
        {
            if (link.button != null)
            {
                SetupButton(link.button, link.sceneName);
            }
        }
    }

    void SetupButton(Button btn, string sceneToLoad)
    {
        // 1. Setup the Click Logic (Load Scene + Sound)
        btn.onClick.AddListener(() => {
            PlaySound(clickSound);
            LoadScene(sceneToLoad);
        });

        // 2. Setup the Hover Logic (The "Satisfying" part)
        // We add an EventTrigger component dynamically so you don't have to do it manually
        EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        // Create the PointerEnter entry
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => { PlaySound(hoverSound); });
        
        trigger.triggers.Add(entry);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || uiAudioSource == null) return;

        // Randomize pitch slightly for that "juicy" satisfying feel
        uiAudioSource.pitch = 1f + Random.Range(-pitchRandomization, pitchRandomization);
        uiAudioSource.PlayOneShot(clip);
    }

    void LoadScene(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"Scene '{sceneName}' cannot be loaded. Check your Build Settings!");
        }
    }
}