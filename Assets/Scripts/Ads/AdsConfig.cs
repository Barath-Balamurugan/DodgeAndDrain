// LevelPlay IDs for Drain Run (from the LevelPlay dashboard: Apps for the app
// key, Setup > Ad units for the rewarded ad unit). They aren't secret.
public static class AdsConfig
{
    // Drain Run's own IDs. If either is ever empty, store builds have no ads (so
    // Revive stays hidden) and test builds use LevelPlay's demo app below.
    public const string AppKey = "295fa6325";
    public const string RewardedAdUnitId = "popd2yyrif9oevtq";

    // Linked from the ad consent card when set (Google Play also requires a
    // privacy policy for apps with ads).
    public const string PrivacyPolicyUrl = "https://sites.google.com/view/drain-run-privacy-policy/home";

    // LevelPlay's public demo app (from the LevelPlay Unity sample). It serves
    // test ads, for trying the integration before Drain Run's IDs exist.
    // Test builds only, never store builds.
    private const string DemoAppKey = "25b63cf85";
    private const string DemoRewardedAdUnitId = "syz3d8ekts22q0or";

    public static bool HasOwnIds => AppKey.Length > 0 && RewardedAdUnitId.Length > 0;

    // The IDs this build uses, or false if it shouldn't show ads at all.
    public static bool TryGetIds(out string appKey, out string rewardedAdUnitId)
    {
        if (HasOwnIds)
        {
            appKey = AppKey;
            rewardedAdUnitId = RewardedAdUnitId;
            return true;
        }

        if (TestBuild.Enabled)
        {
            appKey = DemoAppKey;
            rewardedAdUnitId = DemoRewardedAdUnitId;
            return true;
        }

        appKey = rewardedAdUnitId = null;
        return false;
    }
}
