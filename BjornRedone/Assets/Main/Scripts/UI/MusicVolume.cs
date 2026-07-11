using UnityEngine;

public class Music_slider : MonoBehaviour
{
    private AudioSource musicSource;
    private float appliedVolume = -1f;

    private void Awake()
    {
        musicSource = GetComponent<AudioSource>();
        ApplySavedVolume();
    }

    private void Update()
    {
        ApplySavedVolume();
    }

    private void ApplySavedVolume()
    {
        if (musicSource == null) return;
        float savedVolume = GameSettings.MusicVolume;
        if (Mathf.Approximately(appliedVolume, savedVolume)) return;

        appliedVolume = savedVolume;
        musicSource.volume = savedVolume;
    }
}
