using UnityEngine;
using UnityEngine.UI;

public class MenuMusicSlider : MonoBehaviour
{
    [SerializeField] private Slider musicVolumeSlider; // the slider in the menu
    private bool hasUnsavedChange;

    private void Start()
    {
        if (musicVolumeSlider == null) return;

        // Start slightly below full volume while respecting returning players' preference.
        musicVolumeSlider.value = GameSettings.MusicVolume;

        // Listen to slider changes
        musicVolumeSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        GameSettings.MusicVolume = value;
        hasUnsavedChange = true;
    }

    private void OnDestroy()
    {
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnSliderChanged);

        if (hasUnsavedChange) GameSettings.Save();
    }
}
