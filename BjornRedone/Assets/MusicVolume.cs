using UnityEngine;

public class Music_slider : MonoBehaviour
{
    private AudioSource musicSource;

    private void Awake()
    {
        musicSource = GetComponent<AudioSource>();
        musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f); // load saved volume
    }

    private void Update()
    {
        musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f); // keep it updated
    }
}
