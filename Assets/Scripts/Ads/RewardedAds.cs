using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rewarded video ads ("watch an ad, get a revive"), kept behind this small
// interface so the Game Over screen doesn't depend on any ad SDK.
//
// No ad SDK is installed yet:
// - the editor and test builds (AndroidDeviceBuilder adds DRAINRUN_TEST_ADS)
//   play a short placeholder instead of an ad and then grant the reward, so the
//   revive flow can be tried end to end
// - every other build (store builds) reports no ad available, so Revive stays
//   hidden and players never see a fake ad
//
// To add a real SDK (AdMob, LevelPlay, ...): initialise it and load a rewarded
// ad at startup, return the SDK's "ad loaded" state from IsReady, and in Show
// call onFinished(true) from the SDK's reward callback (or false if the ad
// failed or was closed early) on the main thread, then load the next ad.
public static class RewardedAds
{
    private const float PlaceholderSeconds = 2f;

    public static bool IsShowing { get; private set; }

    public static bool IsReady => UsesPlaceholder && !IsShowing;

    private static bool UsesPlaceholder
    {
        get
        {
#if UNITY_EDITOR || DRAINRUN_TEST_ADS
            return true;
#else
            return false;
#endif
        }
    }

    // Shows an ad; onFinished(true) means the player earned the reward.
    public static void Show(Action<bool> onFinished)
    {
        if (!IsReady)
        {
            onFinished?.Invoke(false);
            return;
        }

        IsShowing = true;
        PlaceholderAd.Play(PlaceholderSeconds, () =>
        {
            IsShowing = false;
            onFinished?.Invoke(true);
        });
    }
}

// Full-screen stand-in for a rewarded video in test builds: a labelled black
// screen with a short countdown. Never used in store builds.
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
        AddText(font, "TEST AD", 64f, new Color(0.955f, 0.955f, 0.955f), new Vector2(0f, 90f));
        AddText(font, "A REAL REWARDED VIDEO WILL PLAY HERE ONCE ADS ARE SET UP", 22f, new Color(0.6f, 0.6f, 0.6f), new Vector2(0f, 10f));
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
