using UnityEngine;

/// <summary>
/// One small, persistent settings surface shared by the menu and gameplay.
/// </summary>
public static class GameSettings
{
    private const string MasterVolumeKey = "MasterVolume";
    private const string MusicVolumeKey = "MusicVolume";
    private const string ScreenShakeKey = "ScreenShake";
    private const string FullscreenKey = "Fullscreen";
    private const string VSyncKey = "VSync";

    public const float DefaultMasterVolume = 1f;
    public const float DefaultMusicVolume = 0.8f;
    public const float DefaultScreenShake = 0.8f;

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        set
        {
            float volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, volume);
            AudioListener.volume = volume;
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
    }

    public static float ScreenShake
    {
        get => PlayerPrefs.GetFloat(ScreenShakeKey, DefaultScreenShake);
        set => PlayerPrefs.SetFloat(ScreenShakeKey, Mathf.Clamp01(value));
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            Screen.fullScreen = value;
        }
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
            QualitySettings.vSyncCount = value ? 1 : 0;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        AudioListener.volume = MasterVolume;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
    }

    public static void ResetToDefaults()
    {
        MasterVolume = DefaultMasterVolume;
        MusicVolume = DefaultMusicVolume;
        ScreenShake = DefaultScreenShake;
        VSync = true;
    }

    public static void Save() => PlayerPrefs.Save();
}
