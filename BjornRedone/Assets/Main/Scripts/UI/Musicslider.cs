using UnityEngine;
using UnityEngine.UI;

public class MenuMusicSlider : MonoBehaviour
{
    private const string MusicVolumeKey = "MusicVolume";
    [SerializeField] private Slider musicVolumeSlider; // the slider in the menu
    private bool hasUnsavedChange;

    private void Start()
    {
        if (musicVolumeSlider == null) return;

        // Start slightly below full volume while respecting returning players' preference.
        float savedVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
        musicVolumeSlider.value = savedVolume;

        // Listen to slider changes
        musicVolumeSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        hasUnsavedChange = true;
    }

    private void OnDestroy()
    {
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnSliderChanged);

        if (hasUnsavedChange) PlayerPrefs.Save();
    }
}
