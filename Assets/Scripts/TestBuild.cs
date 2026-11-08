using UnityEngine;

// Tools for test builds on a phone (AndroidDeviceBuilder adds
// DRAINRUN_TEST_BUILD) and for the editor. Store builds compile them out:
// Enabled is false, the switches always read off and nothing is logged.
public static class TestBuild
{
    private const string ForceRareCardsKey = "Debug.ForceRareCards";

    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR || DRAINRUN_TEST_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    // Every power-up choice offers the rare cards you don't own yet, from
    // wave 1, so all of them can be tried in a few waves.
    public static bool ForceRareCards
    {
        get => Enabled && PlayerPrefs.GetInt(ForceRareCardsKey, 0) == 1;
        set
        {
            if (!Enabled)
                return;
            PlayerPrefs.SetInt(ForceRareCardsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // Pacing numbers for tuning, read with: adb logcat -s Unity | grep DrainRun
    public static void Log(string message)
    {
        if (Enabled)
            Debug.Log("[DrainRun] " + message);
    }
}
