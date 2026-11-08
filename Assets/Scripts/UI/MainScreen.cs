using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The main (start) screen, built in code on top of the existing StartScreen:
// - the arena's charcoal + dots instead of the purple gradient, with a faint
//   pink glow
// - a live preview of the core loop: the player orb in its drain ring pulling
//   in energy while enemies circle
// - a readable title, your best score and wave
// - the existing START button (scene wiring kept) as a glowing PLAY button,
//   plus How to play, sound / vibration toggles and rotating tips
// Lives on the StartScreen object; the layout sits on the root canvas so it can
// use the screen's real edges, and shows / hides with StartScreen.
public class MainScreen : MonoBehaviour
{
    [Header("Tips")]
    [SerializeField, Min(0.5f)] private float secondsPerTip = 3f;
    [SerializeField] private string[] tips =
    {
        "HOLD FIRE TO AUTO-TARGET",
        "DRAIN ENERGY TO REFILL AMMO",
        "STOP DRAINING BEFORE YOU OVERHEAT",
        "CHAIN KILLS FOR UP TO x4 SCORE",
        "ARROWS SHOW ENEMIES OFF SCREEN"
    };

    private static readonly Color Pink = new Color(1f, 0.3f, 0.46f);
    private static readonly Color Cyan = new Color(0.28f, 0.96f, 1f);
    private static readonly Color OffWhite = new Color(0.955f, 0.955f, 0.955f);
    private static readonly Color Muted = new Color(0.6f, 0.6f, 0.6f);
    private const float DrainCycle = 2.6f;
    private const float EntranceDuration = 0.45f;

    private bool built;
    private float time;
    private float entranceTime;

    private RectTransform layout;
    private CanvasGroup layoutGroup;

    private RectTransform ring;
    private RectTransform orbGlow;
    private RectTransform drainable;
    private Image drainableImage;
    private RectTransform beam;
    private Image beamImage;
    private RectTransform enemyA;
    private RectTransform enemyB;

    private RectTransform titleRoot;
    private TMP_Text bestText;
    private RectTransform playButton;
    private Image playGlow;
    private TMP_Text tipText;
    private CanvasGroup tipGroup;
    private int tipIndex;
    private TMP_Text soundLabel;
    private Image soundEdge;
    private TMP_Text vibrationLabel;
    private Image vibrationEdge;
    private TMP_Text rareCardsLabel;
    private TMP_Text euConsentLabel;
    private RectTransform adPrivacyButton;
    private Canvas rootCanvas;
    private TMP_Text uiTextSource;
    private TMP_Text firstRunLabel;
    private float firstRunConfirmTimer;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        if (!built)
            return;

        layout.gameObject.SetActive(true);
        entranceTime = 0f;
        RefreshBest(-1f, -1);
        RefreshToggles();
        RefreshAdPrivacy();
        AskForAdConsentIfNeeded();
    }

    private void OnDisable()
    {
        if (layout != null)
            layout.gameObject.SetActive(false);
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        time += deltaTime;
        entranceTime += deltaTime;
        if (firstRunConfirmTimer > 0f)
        {
            firstRunConfirmTimer -= deltaTime;
            if (firstRunConfirmTimer <= 0f)
                RefreshTestControls();
        }
        Animate();
    }

    // Editor preview: build and show a representative frame.
    public void BuildForPreview(float bestScore, int bestWave, float atTime)
    {
        Build();
        if (layout != null)
            layout.gameObject.SetActive(true);
        RefreshBest(bestScore, bestWave);
        RefreshToggles();
        time = atTime;
        entranceTime = EntranceDuration;
        Animate();
    }

    // Editor preview of the ad consent card over the main screen.
    public void ShowAdConsentForPreview()
    {
        AdConsentCard.Show(rootCanvas, uiTextSource, true, null, true);
    }

    // ---------- Build ----------

    private void Build()
    {
        if (built)
            return;

        Transform panel = transform.Find("Panel");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (panel == null || canvas == null)
            return;

        Transform startButton = panel.Find("Button");
        TMP_Text textSource = startButton != null ? startButton.GetComponentInChildren<TMP_Text>(true) : panel.GetComponentInChildren<TMP_Text>(true);
        if (textSource == null)
            return;

        built = true;
        // Charcoal + drifting random dots + pink glow (shared with How to play).
        ArenaBackdrop.AddTo(panel);

        // Old title goes; the new one lives in the layout.
        Transform oldTitle = panel.Find("Title");
        if (oldTitle != null)
            oldTitle.gameObject.SetActive(false);

        // Full-screen layout on the root canvas, right above the start screen.
        Canvas root = canvas.rootCanvas;
        GameObject layoutObject = new GameObject("MainScreenLayout", typeof(RectTransform), typeof(CanvasGroup));
        layout = layoutObject.GetComponent<RectTransform>();
        layout.SetParent(root.transform, false);
        layout.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        layout.anchorMin = Vector2.zero;
        layout.anchorMax = Vector2.one;
        layout.offsetMin = Vector2.zero;
        layout.offsetMax = Vector2.zero;
        layoutGroup = layoutObject.GetComponent<CanvasGroup>();

        BuildPreviewLoop(layout);
        BuildTitle(layout, textSource);
        BuildPlayButton(layout, startButton, textSource);
        BuildHowToPlay(layout, textSource);
        BuildToggles(layout, textSource);
        BuildTips(layout, textSource);
        if (TestBuild.Enabled)
            BuildTestControls(layout, textSource);

        rootCanvas = root;
        uiTextSource = textSource;
        RefreshAdPrivacy();
        AskForAdConsentIfNeeded();

        layout.gameObject.SetActive(isActiveAndEnabled);
        RefreshBest(-1f, -1);
        RefreshToggles();
    }

    // The orb in its drain ring pulling in energy while two enemies circle.
    private void BuildPreviewLoop(RectTransform parent)
    {
        RectTransform hero = AddRect(parent, "DrainPreview", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(10f, 10f));

        enemyA = AddImage(hero, "EnemyA", NeonUI.Circle, Cyan, Vector2.zero, new Vector2(22f, 22f)).rectTransform;
        AddImage(enemyA, "Glow", NeonUI.SoftDot, WithAlpha(Cyan, 0.45f), Vector2.zero, new Vector2(70f, 70f));
        enemyB = AddImage(hero, "EnemyB", NeonUI.Circle, Cyan, Vector2.zero, new Vector2(22f, 22f)).rectTransform;
        AddImage(enemyB, "Glow", NeonUI.SoftDot, WithAlpha(Cyan, 0.45f), Vector2.zero, new Vector2(70f, 70f));

        beamImage = AddImage(hero, "Beam", null, WithAlpha(Cyan, 0.7f), Vector2.zero, new Vector2(100f, 5f));
        beam = beamImage.rectTransform;
        beam.pivot = new Vector2(0f, 0.5f);

        drainableImage = AddImage(hero, "Energy", NeonUI.Circle, new Color(0.92f, 0.92f, 0.92f), Vector2.zero, new Vector2(22f, 22f));
        drainable = drainableImage.rectTransform;

        ring = AddImage(hero, "DrainRing", NeonUI.DashedRing, WithAlpha(Cyan, 0.6f), Vector2.zero, new Vector2(330f, 330f)).rectTransform;
        orbGlow = AddImage(hero, "OrbGlow", NeonUI.SoftDot, WithAlpha(Pink, 0.55f), Vector2.zero, new Vector2(210f, 210f)).rectTransform;
        AddImage(hero, "Orb", NeonUI.Circle, Pink, Vector2.zero, new Vector2(74f, 74f));
    }

    private void BuildTitle(RectTransform parent, TMP_Text textSource)
    {
        titleRoot = AddRect(parent, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(10f, 10f));

        const float wordGap = 34f;
        TMP_Text drain = AddLabel(textSource, titleRoot, "DRAIN", 80f, Pink, Vector2.zero, new Vector2(700f, 110f),
            TextAlignmentOptions.Right, new Vector2(1f, 0.5f));
        drain.characterSpacing = 28f;
        drain.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, WithAlpha(Pink, 0.55f), 0.12f, 0.3f);

        TMP_Text run = AddLabel(textSource, titleRoot, "RUN", 80f, OffWhite, Vector2.zero, new Vector2(700f, 110f),
            TextAlignmentOptions.Left, new Vector2(0f, 0.5f));
        run.characterSpacing = 28f;
        run.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, new Color(1f, 1f, 1f, 0.25f), 0.1f, 0.28f);

        // Centre the two words as one title ("DRAIN" is wider than "RUN").
        float drainWidth = drain.GetPreferredValues("DRAIN").x;
        float runWidth = run.GetPreferredValues("RUN").x;
        float left = -(drainWidth + wordGap + runWidth) * 0.5f;
        drain.rectTransform.anchoredPosition = new Vector2(left + drainWidth, 0f);
        run.rectTransform.anchoredPosition = new Vector2(left + drainWidth + wordGap, 0f);

        bestText = AddLabel(textSource, parent, string.Empty, 24f, Muted, Vector2.zero, new Vector2(900f, 40f),
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
        Anchor(bestText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -232f));
        bestText.characterSpacing = 14f;
    }

    // The scene's START button, kept (and its click wiring), restyled as PLAY.
    private void BuildPlayButton(RectTransform parent, Transform startButton, TMP_Text textSource)
    {
        if (startButton == null)
            return;

        playGlow = AddImage(parent, "PlayGlow", NeonUI.SoftDot, WithAlpha(Pink, 0.45f), Vector2.zero, new Vector2(640f, 240f));
        Anchor(playGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 205f));

        playButton = (RectTransform)startButton;
        playButton.SetParent(parent, false);
        Anchor(playButton, new Vector2(0.5f, 0f), new Vector2(0f, 205f));
        playButton.sizeDelta = new Vector2(400f, 100f);
        playButton.localScale = Vector3.one;

        TMP_Text label = playButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "PLAY";
            label.fontSize = 38f;
            // Wider spacing opens a visible gap in this font's "AY" pair.
            label.characterSpacing = 16f;
            label.fontFeatures.Clear();
            label.color = Color.white;
        }
    }

    private void BuildHowToPlay(RectTransform parent, TMP_Text textSource)
    {
        Button button = AddOutlineButton(parent, textSource, "HowToPlay", "HOW TO PLAY", new Vector2(0.5f, 0f),
            new Vector2(0f, 92f), new Vector2(330f, 62f), out _, out _);
        button.onClick.AddListener(() =>
        {
            InitialRun initialRun = FindFirstObjectByType<InitialRun>();
            if (initialRun != null)
                initialRun.ShowTutorial();
        });
    }

    private void BuildToggles(RectTransform parent, TMP_Text textSource)
    {
        Button vibration = AddOutlineButton(parent, textSource, "VibrationToggle", string.Empty, new Vector2(1f, 1f),
            new Vector2(-180f, -64f), new Vector2(280f, 58f), out vibrationLabel, out vibrationEdge);
        vibration.onClick.AddListener(() =>
        {
            GameSettings.VibrationOn = !GameSettings.VibrationOn;
            RefreshToggles();
        });

        // EU/UK only: reopens the ad consent card.
        Button adPrivacy = AddOutlineButton(parent, textSource, "AdPrivacy", "AD PRIVACY", new Vector2(1f, 1f),
            new Vector2(-695f, -64f), new Vector2(260f, 58f), out _, out _);
        adPrivacyButton = (RectTransform)adPrivacy.transform;
        adPrivacy.onClick.AddListener(() => AdConsentCard.Show(rootCanvas, uiTextSource, false));

        Button sound = AddOutlineButton(parent, textSource, "SoundToggle", string.Empty, new Vector2(1f, 1f),
            new Vector2(-440f, -64f), new Vector2(210f, 58f), out soundLabel, out soundEdge);
        sound.onClick.AddListener(() =>
        {
            GameSettings.SoundOn = !GameSettings.SoundOn;
            RefreshToggles();
        });
    }

    private void BuildTips(RectTransform parent, TMP_Text textSource)
    {
        tipText = AddLabel(textSource, parent, string.Empty, 20f, Muted, Vector2.zero, new Vector2(760f, 40f),
            TextAlignmentOptions.Left, new Vector2(0f, 0f));
        Anchor(tipText.rectTransform, new Vector2(0f, 0f), new Vector2(60f, 46f));
        tipText.characterSpacing = 6f;
        // Fade the whole line (the coloured "TIP" tag ignores the text's own alpha).
        tipGroup = tipText.gameObject.AddComponent<CanvasGroup>();
        ShowTip(0);
    }

    // Test builds only (never in store builds): amber pills in the bottom-right
    // corner to force the rare power-up cards and to replay the first launch.
    private void BuildTestControls(RectTransform parent, TMP_Text textSource)
    {
        Color amber = new Color(1f, 0.7f, 0.28f);

        Button rares = AddOutlineButton(parent, textSource, "TestRareCards", string.Empty, new Vector2(1f, 0f),
            new Vector2(-215f, 48f), new Vector2(310f, 50f), out rareCardsLabel, out Image rareEdge);
        rareEdge.color = WithAlpha(amber, 0.55f);
        rareCardsLabel.fontSize = 17f;
        rareCardsLabel.color = amber;
        rares.onClick.AddListener(() =>
        {
            TestBuild.ForceRareCards = !TestBuild.ForceRareCards;
            RefreshTestControls();
        });

        Button firstRun = AddOutlineButton(parent, textSource, "TestFirstRun", string.Empty, new Vector2(1f, 0f),
            new Vector2(-545f, 48f), new Vector2(310f, 50f), out firstRunLabel, out Image firstRunEdge);
        firstRunEdge.color = WithAlpha(amber, 0.55f);
        firstRunLabel.fontSize = 17f;
        firstRunLabel.color = amber;
        firstRun.onClick.AddListener(() =>
        {
            InitialRun.ResetFirstRun();
            firstRunConfirmTimer = 1.6f;
            RefreshTestControls();
        });

        Button euConsent = AddOutlineButton(parent, textSource, "TestEuConsent", string.Empty, new Vector2(1f, 0f),
            new Vector2(-215f, 108f), new Vector2(310f, 50f), out euConsentLabel, out Image euEdge);
        euEdge.color = WithAlpha(amber, 0.55f);
        euConsentLabel.fontSize = 17f;
        euConsentLabel.color = amber;
        euConsent.onClick.AddListener(() =>
        {
            TestBuild.ForceEuRegion = !TestBuild.ForceEuRegion;
            if (TestBuild.ForceEuRegion)
                AdsConsent.ClearForTesting();
            RefreshTestControls();
            RefreshAdPrivacy();
            AskForAdConsentIfNeeded();
        });

        RefreshTestControls();
    }

    // Before the first play in the EU/UK: ask about ads (once).
    private void AskForAdConsentIfNeeded()
    {
        if (AdsConsent.NeedsAnswer && !AdConsentCard.IsOpen && rootCanvas != null)
            AdConsentCard.Show(rootCanvas, uiTextSource, true);
    }

    private void RefreshAdPrivacy()
    {
        if (adPrivacyButton != null)
            adPrivacyButton.gameObject.SetActive(AdsConsent.InRegulatedRegion);
    }

    private void RefreshTestControls()
    {
        if (rareCardsLabel != null)
            rareCardsLabel.text = TestBuild.ForceRareCards ? "TEST  RARES FORCED" : "TEST  RARES NORMAL";
        if (firstRunLabel != null)
            firstRunLabel.text = firstRunConfirmTimer > 0f ? "TEST  FIRST RUN SET" : "TEST  RESET FIRST RUN";
        if (euConsentLabel != null)
            euConsentLabel.text = TestBuild.ForceEuRegion ? "TEST  EU CONSENT ON" : "TEST  EU CONSENT OFF";
    }

    // ---------- State ----------

    // Negative values: read the saved bests.
    private void RefreshBest(float bestScore, int bestWave)
    {
        if (bestText == null)
            return;

        if (bestScore < 0f)
            bestScore = GameManager.Instance != null ? GameManager.Instance.bestScore : PlayerPrefs.GetFloat("BestScore", 0f);
        if (bestWave < 0)
            bestWave = GameSettings.BestWave;

        int score = Mathf.RoundToInt(bestScore);
        if (score <= 0 && bestWave <= 0)
        {
            bestText.text = string.Empty;
            return;
        }

        string scorePart = $"BEST <color=#F4F4F4>{score}</color>";
        bestText.text = bestWave > 0
            ? $"{scorePart}   <color=#FF4C76>/</color>   WAVE <color=#F4F4F4>{bestWave}</color>"
            : scorePart;
    }

    private void RefreshToggles()
    {
        StyleToggle(soundLabel, soundEdge, "SOUND", GameSettings.SoundOn);
        StyleToggle(vibrationLabel, vibrationEdge, "VIBRATION", GameSettings.VibrationOn);
    }

    private static void StyleToggle(TMP_Text label, Image edge, string name, bool on)
    {
        if (label == null || edge == null)
            return;

        label.text = on ? $"{name} ON" : $"{name} OFF";
        label.color = on ? OffWhite : new Color(0.45f, 0.45f, 0.45f);
        edge.color = on ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.22f);
    }

    private void ShowTip(int index)
    {
        if (tipText == null || tips == null || tips.Length == 0)
            return;

        tipIndex = (index % tips.Length + tips.Length) % tips.Length;
        tipText.text = $"<color=#48F4FF>TIP</color>   {tips[tipIndex]}";
    }

    // ---------- Animation ----------

    private void Animate()
    {
        if (!built)
            return;

        float entrance = Mathf.Clamp01(entranceTime / EntranceDuration);
        float eased = 1f - Mathf.Pow(1f - entrance, 3f);
        if (layoutGroup != null)
            layoutGroup.alpha = eased;
        if (titleRoot != null)
            titleRoot.anchoredPosition = new Vector2(0f, -150f + 24f * (1f - eased));

        if (ring != null)
            ring.localRotation = Quaternion.Euler(0f, 0f, -time * 36f);
        if (orbGlow != null)
            orbGlow.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(time * 3.5f));

        AnimateDrain();

        if (enemyA != null)
            enemyA.anchoredPosition = Ellipse(time * 0.9f, 470f, 160f);
        if (enemyB != null)
            enemyB.anchoredPosition = Ellipse(-time * 0.7f + 3.4f, 640f, 205f);

        if (playButton != null)
        {
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2.9f);
            playButton.localScale = Vector3.one * (1f + 0.035f * breathe);
            if (playGlow != null)
                playGlow.color = WithAlpha(Pink, Mathf.Lerp(0.3f, 0.6f, breathe));
        }

        AnimateTips();
    }

    // Energy drifts in from the upper right along a beam and is absorbed.
    private void AnimateDrain()
    {
        if (drainable == null || beam == null)
            return;

        float phase = (time % DrainCycle) / DrainCycle;
        Vector2 start = new Vector2(330f, 150f);
        Vector2 position = Vector2.Lerp(start, Vector2.zero, phase * phase);
        float alpha = Mathf.Clamp01(phase / 0.15f) * Mathf.Clamp01((1f - phase) / 0.15f);

        drainable.anchoredPosition = position;
        drainableImage.color = WithAlpha(drainableImage.color, alpha);

        float length = position.magnitude;
        beam.sizeDelta = new Vector2(length, 5f);
        beam.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg);
        beamImage.color = WithAlpha(Cyan, 0.65f * alpha);
    }

    private void AnimateTips()
    {
        if (tipText == null || tips == null || tips.Length == 0)
            return;

        int index = Mathf.FloorToInt(time / secondsPerTip);
        if (index % tips.Length != tipIndex)
            ShowTip(index);

        float within = (time % secondsPerTip) / secondsPerTip;
        float fade = Mathf.Clamp01(within / 0.12f) * Mathf.Clamp01((1f - within) / 0.12f);
        if (tipGroup != null)
            tipGroup.alpha = fade;
    }

    private static Vector2 Ellipse(float angle, float radiusX, float radiusY)
    {
        return new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
    }

    // ---------- Helpers ----------

    private static RectTransform AddRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        RectTransform rect = element.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
    {
        RectTransform rect = AddRect(parent, name, new Vector2(0.5f, 0.5f), position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text AddLabel(TMP_Text textSource, Transform parent, string text, float fontSize, Color color,
        Vector2 position, Vector2 size, TextAlignmentOptions alignment, Vector2 pivot)
    {
        TMP_Text label = Instantiate(textSource, parent, false);
        label.gameObject.name = "Label";
        label.text = text;
        label.richText = true;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return label;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Pill-shaped outline button (white edge, transparent inside, whole pill tappable).
    private static Button AddOutlineButton(RectTransform parent, TMP_Text textSource, string name, string text,
        Vector2 anchor, Vector2 position, Vector2 size, out TMP_Text label, out Image edge)
    {
        RectTransform rect = AddRect(parent, name, anchor, position, size);

        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0.001f);
        hit.raycastTarget = true;

        edge = AddImage(rect, "Edge", NeonUI.RoundedOutline, new Color(1f, 1f, 1f, 0.6f), Vector2.zero, size);
        edge.type = Image.Type.Sliced;
        // The sprite's corners are 40 units; scale them to a full pill.
        edge.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / (size.y * 0.5f);

        label = AddLabel(textSource, rect, text, 22f, OffWhite, Vector2.zero, size, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
        label.characterSpacing = 14f;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
