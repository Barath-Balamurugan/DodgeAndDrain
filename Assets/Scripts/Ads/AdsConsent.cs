using UnityEngine;

// Ad consent for players in the EU/EEA, UK and Switzerland, where personalised
// ads need the player's permission first. Everywhere else, ads start without
// asking. Until a player in those regions has answered, ads stay off (Revive is
// hidden), so nothing is shown or loaded without consent.
//
// The region comes from the phone's country setting and time zone, which is an
// approximation (a phone set to Germany, or on a European time zone, counts).
public static class AdsConsent
{
    private const string ConsentKey = "Ads.Consent"; // 1 = yes, 0 = no, missing = not asked

    private static readonly string[] RegulatedCountries =
    {
        // EU
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE",
        "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
        // rest of the EEA, the UK and Switzerland
        "IS", "LI", "NO", "GB", "CH"
    };

    private static bool? inRegulatedRegion;

    // Test builds can pretend to be in the EU (TestBuild.ForceEuRegion).
    public static bool InRegulatedRegion => TestBuild.ForceEuRegion || (inRegulatedRegion ??= DetectRegulatedRegion());

    // True when the consent card must be answered before ads can start.
    public static bool NeedsAnswer => InRegulatedRegion && !HasAnswered;

    // True once ads may start: outside those regions always, inside them after
    // the player has answered (either way).
    public static bool IsSettled => !InRegulatedRegion || PlayerPrefs.HasKey(ConsentKey);

    public static bool HasAnswered => PlayerPrefs.HasKey(ConsentKey);

    // The player's answer (only meaningful once HasAnswered).
    public static bool Granted => PlayerPrefs.GetInt(ConsentKey, 0) == 1;

    // Test builds: forget the answer so the card shows again.
    public static void ClearForTesting()
    {
        PlayerPrefs.DeleteKey(ConsentKey);
        PlayerPrefs.Save();
    }

    public static void SetGranted(bool granted)
    {
        PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
        PlayerPrefs.Save();
        RewardedAds.OnConsentChanged();
    }

    private static bool DetectRegulatedRegion()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass localeClass = new AndroidJavaClass("java.util.Locale"))
            using (AndroidJavaObject locale = localeClass.CallStatic<AndroidJavaObject>("getDefault"))
            {
                string country = locale.Call<string>("getCountry");
                if (!string.IsNullOrEmpty(country) && System.Array.IndexOf(RegulatedCountries, country.ToUpperInvariant()) >= 0)
                    return true;
            }

            using (AndroidJavaClass timeZoneClass = new AndroidJavaClass("java.util.TimeZone"))
            using (AndroidJavaObject timeZone = timeZoneClass.CallStatic<AndroidJavaObject>("getDefault"))
            {
                string id = timeZone.Call<string>("getID") ?? string.Empty;
                // Europe/... covers the EU, EEA, UK and Switzerland (plus a few
                // non-EU countries, where asking does no harm).
                if (id.StartsWith("Europe/") || id == "Atlantic/Reykjavik" || id == "Atlantic/Canary" || id == "Atlantic/Madeira" || id == "Atlantic/Azores")
                    return true;
            }
        }
        catch (System.Exception exception)
        {
            // If the region can't be read, ask to be safe.
            Debug.LogWarning("Ad consent region check failed: " + exception.Message);
            return true;
        }

        return false;
#else
        return false;
#endif
    }
}
