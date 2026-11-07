using UnityEngine;

// Player-facing settings, saved between launches (PlayerPrefs).
// Sound mutes everything through the AudioListener; vibration gates the
// haptics in GameFeedback and the death buzz in GameManager.
public static class GameSettings
{
    private const string SoundKey = "Settings.SoundOn";
    private const string VibrationKey = "Settings.VibrationOn";
    private const string BestWaveKey = "BestWave";

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplySound();
        }
    }

    public static bool VibrationOn
    {
        get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // Highest wave reached in any run.
    public static int BestWave => PlayerPrefs.GetInt(BestWaveKey, 0);

    public static void RecordWaveReached(int wave)
    {
        if (wave <= BestWave)
            return;

        PlayerPrefs.SetInt(BestWaveKey, wave);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySound()
    {
        AudioListener.volume = SoundOn ? 1f : 0f;
    }
}
