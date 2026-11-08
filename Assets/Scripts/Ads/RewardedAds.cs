using System;
using System.Collections;
using System.Collections.Concurrent;
using TMPro;
using Unity.Services.LevelPlay;
using UnityEngine;
using UnityEngine.UI;

// Rewarded video ads ("watch an ad, get a revive") through Unity LevelPlay,
// kept behind this small interface so the Game Over screen doesn't depend on
// the SDK.
// - On phones: LevelPlay. Drain Run's IDs come from AdsConfig; until they are
//   filled in, store builds have no ads (Revive stays hidden) and test builds
//   use LevelPlay's demo app.
// - In the editor: a 2-second placeholder (LevelPlay doesn't run there).
// - Consent: in the EU/EEA, UK and Switzerland the SDK only starts once the
//   player has answered (AdsConsent).
public static class RewardedAds
{
    private const float PlaceholderSeconds = 2f;

    public static bool IsShowing { get; private set; }

    public static bool IsReady => !IsShowing && (UsesPlaceholder || LevelPlayRewardedRunner.IsAdLoaded);

    private static bool UsesPlaceholder
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartOnLaunch()
    {
        StartIfAllowed();
    }

    // Called by AdsConsent when the player answers.
    public static void OnConsentChanged()
    {
        LevelPlayRewardedRunner.ApplyConsent();
        StartIfAllowed();
    }

    private static void StartIfAllowed()
    {
        if (UsesPlaceholder || !AdsConsent.IsSettled)
            return;

        if (AdsConfig.TryGetIds(out string appKey, out string adUnitId))
            LevelPlayRewardedRunner.Start(appKey, adUnitId);
    }

    // Shows an ad; onFinished(true) means the player earned the reward.
    // Always called back on the main thread.
    public static void Show(Action<bool> onFinished)
    {
        if (!IsReady)
        {
            onFinished?.Invoke(false);
            return;
        }

        IsShowing = true;
        void Finish(bool rewarded)
        {
            IsShowing = false;
            onFinished?.Invoke(rewarded);
        }

        if (UsesPlaceholder)
            PlaceholderAd.Play(PlaceholderSeconds, () => Finish(true));
        else
            LevelPlayRewardedRunner.Show(Finish);
    }
}

// Owns the LevelPlay SDK and the one rewarded ad: starts the SDK, keeps an ad
// loaded (retrying with a growing delay when there's no fill), shows it, and
// reports whether it was rewarded. SDK callbacks are moved onto the main thread.
internal sealed class LevelPlayRewardedRunner : MonoBehaviour
{
    private const float FirstRetryDelay = 4f;
    private const float MaxRetryDelay = 64f;
    // Some networks report the reward just after the ad closes.
    private const float LateRewardGrace = 1f;

    private static LevelPlayRewardedRunner instance;

    private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
    private string adUnitId;
    private LevelPlayRewardedAd rewardedAd;
    private float retryDelay = FirstRetryDelay;
    private Action<bool> pendingShow;
    private bool rewarded;
    private bool closed;

    public static bool IsAdLoaded => instance != null && instance.rewardedAd != null && instance.rewardedAd.IsAdReady();

    public static void Start(string appKey, string rewardedAdUnitId)
    {
        if (instance != null)
            return;

        GameObject runner = new GameObject("LevelPlayRewardedAds");
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<LevelPlayRewardedRunner>();
        instance.adUnitId = rewardedAdUnitId;

        ApplyConsent();
        // Pause the game while an ad is on screen; it resumes when the ad closes.
        LevelPlay.SetPauseGame(true);
        if (TestBuild.Enabled)
            LevelPlay.SetAdaptersDebug(true);

        LevelPlay.OnInitSuccess += instance.OnInitSuccess;
        LevelPlay.OnInitFailed += instance.OnInitFailed;
        LevelPlay.Init(appKey);
    }

    // Passes the player's answer to LevelPlay (only asked in regulated regions).
    public static void ApplyConsent()
    {
        if (AdsConsent.InRegulatedRegion && AdsConsent.HasAnswered)
            LevelPlayPrivacySettings.SetGDPRConsent(AdsConsent.Granted);
    }

    public static void Show(Action<bool> onFinished)
    {
        if (!IsAdLoaded)
        {
            onFinished?.Invoke(false);
            return;
        }

        instance.pendingShow = onFinished;
        instance.rewarded = false;
        instance.closed = false;
        instance.rewardedAd.ShowAd();
    }

    private void Update()
    {
        while (mainThread.TryDequeue(out Action action))
            action();
    }

    private void Post(Action action) => mainThread.Enqueue(action);

    private void OnInitSuccess(LevelPlayConfiguration configuration) => Post(() =>
    {
        TestBuild.Log("LevelPlay ready");
        if (TestBuild.Enabled)
            LevelPlay.ValidateIntegration();

        rewardedAd = new LevelPlayRewardedAd(adUnitId);
        rewardedAd.OnAdLoaded += info => Post(() =>
        {
            retryDelay = FirstRetryDelay;
            TestBuild.Log("rewarded ad loaded");
        });
        rewardedAd.OnAdLoadFailed += error => Post(() =>
        {
            TestBuild.Log($"rewarded ad load failed: {error}; retrying in {retryDelay:F0}s");
            StartCoroutine(LoadAfter(retryDelay));
            retryDelay = Mathf.Min(retryDelay * 2f, MaxRetryDelay);
        });
        rewardedAd.OnAdRewarded += (info, reward) => Post(() =>
        {
            rewarded = true;
            if (closed)
                FinishShow();
        });
        rewardedAd.OnAdClosed += info => Post(() =>
        {
            closed = true;
            if (rewarded)
                FinishShow();
            else
                StartCoroutine(FinishAfterGrace());
        });
        rewardedAd.OnAdDisplayFailed += (info, error) => Post(() =>
        {
            TestBuild.Log($"rewarded ad failed to show: {error}");
            rewarded = false;
            FinishShow();
        });
        rewardedAd.LoadAd();
    });

    private void OnInitFailed(LevelPlayInitError error) => Post(() =>
    {
        TestBuild.Log($"LevelPlay init failed: {error}");
    });

    private IEnumerator LoadAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (rewardedAd != null && !rewardedAd.IsAdReady())
            rewardedAd.LoadAd();
    }

    private IEnumerator FinishAfterGrace()
    {
        float waited = 0f;
        while (!rewarded && waited < LateRewardGrace)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        FinishShow();
    }

    // Reports the result once, then loads the next ad.
    private void FinishShow()
    {
        Action<bool> callback = pendingShow;
        pendingShow = null;
        if (callback == null)
            return;

        callback(rewarded);
        rewardedAd.LoadAd();
    }
}

// Full-screen stand-in for a rewarded video in the editor: a labelled black
// screen with a short countdown. Never used on phones.
public class PlaceholderAd : MonoBehaviour
{
    private float duration;
    private Action onComplete;
    private TMP_Text countdown;
    private RectTransform progressFill;

    public static void Play(float seconds, Action onComplete)
    {
        GameObject root = new GameObject("PlaceholderAd", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        PlaceholderAd ad = root.AddComponent<PlaceholderAd>();
        ad.duration = seconds;
        ad.onComplete = onComplete;
        ad.Build();
    }

    private void Build()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;

        // Black backdrop that also swallows every touch while the "ad" plays.
        Image backdrop = AddRect("Backdrop", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        Stretch(backdrop.rectTransform);
        backdrop.color = Color.black;

        TMP_FontAsset font = FindGameFont();
        AddText(font, "EDITOR AD", 64f, new Color(0.955f, 0.955f, 0.955f), new Vector2(0f, 90f));
        AddText(font, "ON A PHONE, A LEVELPLAY REWARDED VIDEO PLAYS HERE", 22f, new Color(0.6f, 0.6f, 0.6f), new Vector2(0f, 10f));
        countdown = AddText(font, string.Empty, 26f, new Color(0.28f, 0.96f, 1f), new Vector2(0f, -70f));

        RectTransform track = AddRect("Progress", new Vector2(0f, -130f), new Vector2(520f, 6f));
        track.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        progressFill = AddRect("Fill", Vector2.zero, Vector2.zero, track);
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        progressFill.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.96f, 1f);

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            countdown.text = $"REVIVING IN {Mathf.CeilToInt(duration - elapsed)}";
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(elapsed / duration), 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Action complete = onComplete;
        Destroy(gameObject);
        complete?.Invoke();
    }

    private static TMP_FontAsset FindGameFont()
    {
        TMP_Text sceneText = FindFirstObjectByType<TMP_Text>();
        return sceneText != null ? sceneText.font : TMP_Settings.defaultFontAsset;
    }

    private RectTransform AddRect(string name, Vector2 position, Vector2 size, Transform parent = null)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        RectTransform rect = element.GetComponent<RectTransform>();
        rect.SetParent(parent != null ? parent : transform, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private TMP_Text AddText(TMP_FontAsset font, string text, float fontSize, Color color, Vector2 position)
    {
        RectTransform rect = AddRect("Text", position, new Vector2(1600f, 80f));
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = 10f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
