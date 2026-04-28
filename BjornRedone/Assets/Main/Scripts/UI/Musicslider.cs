using UnityEngine;
using UnityEngine.UI;

public class MenuMusicSlider : MonoBehaviour
{
    [SerializeField] private Slider musicVolumeSlider; // the slider in the menu

    private void Start()
    {
        if (musicVolumeSlider == null) return;

        // Load saved volume or default to 1
        float savedVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        musicVolumeSlider.value = savedVolume;

        // Listen to slider changes
        musicVolumeSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        // Save the new volume to PlayerPrefs
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
    }
}
