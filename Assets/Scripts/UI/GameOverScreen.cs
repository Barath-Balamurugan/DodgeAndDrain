using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Everything the Game Over screen shows about a finished (or paused-by-death) run.
public struct GameOverResult
{
    public int score;
    public int bestScore;
    public bool newBest;
    public int wave;
    public int kills;
    public int bestMultiplier;
    public float seconds;
    public string tip;
    public bool canRevive;
}

// The Game Over screen, built in code on top of the scene's EndScreen:
// - the frozen arena behind a dark dim with a red edge glow
// - GAME OVER, the wave you reached and a score that counts up, with a NEW
//   BEST badge (or your best underneath)
// - wave / kills / best streak / time tiles and a tip based on how you died
// - the scene's Menu and Restart buttons (wiring kept) plus Revive, which
//   plays a rewarded ad first (once per run, only when an ad is ready)
// GameManager attaches it at startup and calls Show on death. The layout sits
// on the root canvas, sorted above the HUD and wave text, and shows / hides
// with EndScreen.
public class GameOverScreen : MonoBehaviour
{
    private static readonly Color Pink = new Color(1f, 0.3f, 0.46f);
    private static readonly Color Cyan = new Color(0.28f, 0.96f, 1f);
    private static readonly Color Amber = new Color(1f, 0.7f, 0.28f);
    private static readonly Color OffWhite = new Color(0.955f, 0.955f, 0.955f);
    private static readonly Color Muted = new Color(0.6f, 0.6f, 0.6f);
    private static readonly Color Ink = new Color(0.08f, 0.08f, 0.08f);
    private static readonly Color TileFill = new Color(0.09f, 0.09f, 0.1f, 1f);
    private static readonly Color TileEdge = new Color(0.19f, 0.19f, 0.21f, 1f);

    // Let the death burst play before the screen fades in.
    private const float ShowDelay = 0.45f;
    private const float FadeDuration = 0.35f;
    private const float CountDelay = 0.1f;
    private const float CountDuration = 0.9f;
    private const float BadgePopDuration = 0.3f;
    private const int SortingOrder = 400;

    // Vertical placement as a fraction of screen height, so the layout fits
    // both 16:9 and wide phones (on this canvas wide phones are shorter).
    private const float TitleY = 0.885f;
    private const float ReachedY = 0.8f;
    private const float ScoreY = 0.665f;
    private const float BestY = 0.535f;
    private const float StatsY = 0.405f;
    private const float TipY = 0.27f;
    private const float ButtonsY = 0.11f;

    private static readonly Vector2 TileSize = new Vector2(220f, 104f);
    private const float TileGap = 22f;
    private const float ButtonGap = 26f;

    private bool built;
    private GameOverResult result;
    private float time;
    private bool reviving;

    private RectTransform layout;
    private CanvasGroup layoutGroup;
    private RectTransform title;
    private TMP_Text reachedText;
    private TMP_Text scoreText;
    private RectTransform badge;
    private TMP_Text bestText;
    private readonly TMP_Text[] statValues = new TMP_Text[4];
    private TMP_Text tipText;

    private RectTransform buttonRow;
    private Button menuButton;
    private Button restartButton;
    private Button reviveButton;
    private Image restartFill;
    private TMP_Text restartLabel;
    private Image primaryGlow;

    // Gameplay HUD (joystick, buttons, score, heat gauge), hidden while shown.
    private static readonly string[] HudNames = { "UI", "DrainGauge" };
    private readonly System.Collections.Generic.List<CanvasGroup> hudGroups = new System.Collections.Generic.List<CanvasGroup>();

    public static GameOverScreen AttachTo(GameObject endScreen)
    {
        GameOverScreen screen = endScreen.GetComponent<GameOverScreen>();
        if (screen == null)
            screen = endScreen.AddComponent<GameOverScreen>();
        screen.Build();
        return screen;
    }

    private void OnEnable()
    {
        if (layout != null)
            layout.gameObject.SetActive(true);
        SetHudHidden(true);
    }

    private void OnDisable()
    {
        if (layout != null)
            layout.gameObject.SetActive(false);
        SetHudHidden(false);
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        Animate();
    }

    // Fills the screen in for this death and replays its entrance.
    public void Show(GameOverResult newResult, bool skipEntrance = false)
    {
        Build();
        if (!built)
            return;

        result = newResult;
        reviving = false;
        layout.gameObject.SetActive(true);
        SetHudHidden(true);

        title.anchoredPosition = Vector2.zero;
        reachedText.text = $"REACHED <color=#F4F4F4>WAVE {Mathf.Max(1, result.wave)}</color>";
        bestText.text = $"BEST <color=#F4F4F4>{result.bestScore}</color>";
        badge.gameObject.SetActive(result.newBest);
        bestText.gameObject.SetActive(!result.newBest);

        statValues[0].text = Mathf.Max(1, result.wave).ToString();
        statValues[1].text = result.kills.ToString();
        statValues[2].text = $"x{Mathf.Max(1, result.bestMultiplier)}";
        statValues[2].color = StreakColor(result.bestMultiplier);
        statValues[3].text = FormatTime(result.seconds);

        tipText.text = string.IsNullOrEmpty(result.tip) ? string.Empty : $"<color=#48F4FF>TIP</color>   {result.tip}";

        LayoutButtons();
        SetButtonsInteractable(true);

        time = skipEntrance ? ShowDelay + CountDelay + CountDuration + BadgePopDuration + 1f : 0f;
        Animate();
    }

    // Revive: watch a rewarded ad, then come back (once per run).
    public void RequestRevive()
    {
        if (reviving || !result.canRevive || !RewardedAds.IsReady)
            return;

        reviving = true;
        SetButtonsInteractable(false);
        RewardedAds.Show(rewarded =>
        {
            reviving = false;
            GameManager gameManager = GameManager.Instance;
            if (rewarded && gameManager != null && gameManager.IsGameOver)
                gameManager.RevivePlayer();
            else
                SetButtonsInteractable(true);
        });
    }

    // Short advice for the next run, based on how this one ended.
    public static string TipFor(DeathCause cause, bool outOfAmmo, bool wasDraining)
    {
        if (outOfAmmo)
            return "YOU RAN OUT OF AMMO. DRAIN WHITE ENERGY IN YOUR RING TO REFILL.";
        if (wasDraining)
            return "DRAINING SLOWS YOU DOWN. LET GO WHEN ENEMIES GET CLOSE.";

        switch (cause)
        {
            case DeathCause.EnemyShot:
                return "SHOOTERS SHOW A WARNING LINE BEFORE THEY FIRE. MOVE OFF IT.";
            case DeathCause.Dasher:
                return "DASHERS FLASH AND AIM BEFORE THEY CHARGE. STEP OUT OF THEIR PATH.";
            case DeathCause.Shooter:
                return "SHOOTERS HURT ON CONTACT TOO. KEEP YOUR DISTANCE.";
            case DeathCause.Chaser:
                return "CHASERS FOLLOW YOU. SHOOT THEM BEFORE THEY GET CLOSE.";
            default:
                return "CHAIN KILLS QUICKLY FOR UP TO x4 SCORE.";
        }
    }

    // ---------- Build ----------

    private void Build()
    {
        if (built)
            return;

        Transform panel = transform.Find("Panel");
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (panel == null || canvas == null)
            return;

        restartButton = FindButton(panel, "Restart_Button");
        reviveButton = FindButton(panel, "Revive_Button");
        menuButton = FindButton(panel, "Menu_Button");
        TMP_Text textSource = restartButton != null ? restartButton.GetComponentInChildren<TMP_Text>(true) : panel.GetComponentInChildren<TMP_Text>(true);
        if (textSource == null)
            return;

        built = true;

        // The layout draws its own dim; the old grey panel goes.
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.enabled = false;

        // Full-screen layout on the root canvas with its own sorting, above the
        // HUD and the wave text (which has its own canvas too).
        Canvas root = canvas.rootCanvas;
        GameObject layoutObject = new GameObject("GameOverLayout", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        layout = layoutObject.GetComponent<RectTransform>();
        layout.SetParent(root.transform, false);
        layout.SetAsLastSibling();
        Stretch(layout);
        Canvas layoutCanvas = layoutObject.GetComponent<Canvas>();
        layoutCanvas.overrideSorting = true;
        layoutCanvas.sortingOrder = SortingOrder;
        layoutGroup = layoutObject.GetComponent<CanvasGroup>();

        foreach (string hudName in HudNames)
        {
            Transform hud = root.transform.Find(hudName);
            if (hud == null)
                continue;
            CanvasGroup group = hud.GetComponent<CanvasGroup>();
            hudGroups.Add(group != null ? group : hud.gameObject.AddComponent<CanvasGroup>());
        }

        BuildBackdrop();
        BuildHeader(textSource);
        BuildStats(textSource);
        BuildButtons(textSource);

        layout.gameObject.SetActive(isActiveAndEnabled);
    }

    private void BuildBackdrop()
    {
        // Dim the frozen arena; also blocks touches to the HUD underneath.
        Image dim = AddImage(layout, "Dim", null, new Color(0.02f, 0.012f, 0.016f, 0.9f), Vector2.zero, Vector2.zero);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // Faint red glow around the screen edge (linear colour space: small
        // alphas already read strongly).
        Image edge = AddImage(layout, "RedEdge", NeonUI.EdgeVignette, new Color(1f, 0.2f, 0.32f, 0.13f), Vector2.zero, Vector2.zero);
        Stretch(edge.rectTransform);
        edge.type = Image.Type.Sliced;
        edge.fillCenter = true;
        // The sprite's glow is 128 units deep; spread it over 220.
        edge.pixelsPerUnitMultiplier = NeonUI.EdgeVignetteSpread / 220f;
    }

    private void BuildHeader(TMP_Text textSource)
    {
        title = AddRect(layout, "Title", new Vector2(0.5f, TitleY), Vector2.zero, new Vector2(10f, 10f));
        TMP_Text titleLabel = AddLabel(textSource, title, "GAME OVER", 64f, Pink, Vector2.zero, new Vector2(1200f, 100f));
        titleLabel.characterSpacing = 30f;
        titleLabel.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, WithAlpha(Pink, 0.55f), 0.12f, 0.3f);

        reachedText = AddLabel(textSource, layout, string.Empty, 24f, Muted, Vector2.zero, new Vector2(900f, 40f));
        Anchor(reachedText.rectTransform, new Vector2(0.5f, ReachedY), Vector2.zero);
        reachedText.characterSpacing = 14f;

        scoreText = AddLabel(textSource, layout, "0", 120f, Color.white, Vector2.zero, new Vector2(1000f, 160f));
        Anchor(scoreText.rectTransform, new Vector2(0.5f, ScoreY), Vector2.zero);
        scoreText.characterSpacing = 4f;
        scoreText.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, new Color(1f, 1f, 1f, 0.25f), 0.1f, 0.3f);

        // NEW BEST: an amber pill that pops in once the score finishes counting.
        badge = AddRect(layout, "NewBest", new Vector2(0.5f, BestY), Vector2.zero, new Vector2(220f, 46f));
        AddImage(badge, "Glow", NeonUI.SoftDot, WithAlpha(Amber, 0.45f), Vector2.zero, new Vector2(360f, 130f));
        Image pill = AddImage(badge, "Pill", NeonUI.RoundedRect, Amber, Vector2.zero, new Vector2(220f, 46f));
        MakePill(pill, 46f);
        TMP_Text badgeLabel = AddLabel(textSource, badge, "NEW BEST", 21f, new Color(0.1f, 0.075f, 0.024f), Vector2.zero, new Vector2(220f, 46f));
        badgeLabel.characterSpacing = 14f;

        bestText = AddLabel(textSource, layout, string.Empty, 22f, Muted, Vector2.zero, new Vector2(700f, 40f));
        Anchor(bestText.rectTransform, new Vector2(0.5f, BestY), Vector2.zero);
        bestText.characterSpacing = 14f;
    }

    private void BuildStats(TMP_Text textSource)
    {
        RectTransform row = AddRect(layout, "Stats", new Vector2(0.5f, StatsY), Vector2.zero, new Vector2(10f, 10f));
        string[] names = { "WAVE", "KILLS", "BEST STREAK", "TIME" };
        for (int i = 0; i < names.Length; i++)
        {
            float x = (i - (names.Length - 1) * 0.5f) * (TileSize.x + TileGap);
            RectTransform tile = AddRect(row, names[i], new Vector2(0.5f, 0.5f), new Vector2(x, 0f), TileSize);

            Image fill = AddImage(tile, "Fill", NeonUI.RoundedRect, TileFill, Vector2.zero, TileSize);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / 16f;
            Image edge = AddImage(tile, "Edge", NeonUI.RoundedOutline, TileEdge, Vector2.zero, TileSize);
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / 16f;

            statValues[i] = AddLabel(textSource, tile, "0", 40f, OffWhite, new Vector2(0f, 14f), new Vector2(TileSize.x, 50f));
            TMP_Text label = AddLabel(textSource, tile, names[i], 16f, Muted, new Vector2(0f, -30f), new Vector2(TileSize.x, 24f));
            label.characterSpacing = 10f;
        }

        tipText = AddLabel(textSource, layout, string.Empty, 21f, Muted, Vector2.zero, new Vector2(1600f, 40f));
        Anchor(tipText.rectTransform, new Vector2(0.5f, TipY), Vector2.zero);
        tipText.characterSpacing = 6f;
    }

    // The scene's buttons move into one centred row: MENU, RESTART, REVIVE.
    private void BuildButtons(TMP_Text textSource)
    {
        buttonRow = AddRect(layout, "Buttons", new Vector2(0.5f, ButtonsY), Vector2.zero, new Vector2(10f, 10f));

        // Glow behind whichever button is the main action (Revive, else Restart).
        primaryGlow = AddImage(buttonRow, "PrimaryGlow", NeonUI.SoftDot, WithAlpha(Pink, 0.45f), Vector2.zero, new Vector2(560f, 210f));

        if (menuButton != null)
        {
            RectTransform rect = AdoptButton(menuButton, new Vector2(210f, 64f));
            Image hit = menuButton.GetComponent<Image>();
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            Image edge = AddImage(rect, "Edge", NeonUI.RoundedOutline, new Color(1f, 1f, 1f, 0.6f), Vector2.zero, rect.sizeDelta);
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / (rect.sizeDelta.y * 0.5f);
            edge.transform.SetAsFirstSibling();
            menuButton.targetGraphic = edge;
            StyleLabel(menuButton, "MENU", 22f, OffWhite);
        }

        if (restartButton != null)
        {
            AdoptButton(restartButton, new Vector2(300f, 76f));
            restartFill = restartButton.GetComponent<Image>();
            restartFill.sprite = NeonUI.RoundedRect;
            MakePill(restartFill, 76f);
            restartLabel = StyleLabel(restartButton, "RESTART", 24f, OffWhite);
        }

        if (reviveButton != null)
        {
            RectTransform rect = AdoptButton(reviveButton, new Vector2(350f, 76f));
            Image fill = reviveButton.GetComponent<Image>();
            fill.sprite = NeonUI.RoundedRect;
            fill.color = Pink;
            MakePill(fill, 76f);
            TMP_Text label = StyleLabel(reviveButton, "REVIVE", 24f, Color.white);

            // "▶ AD" chip: Revive costs one rewarded video.
            RectTransform chip = AddRect(rect, "AdChip", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 40f));
            Image chipFill = AddImage(chip, "Fill", NeonUI.RoundedRect, new Color(1f, 1f, 1f, 0.24f), Vector2.zero, chip.sizeDelta);
            MakePill(chipFill, 40f);
            AddImage(chip, "Play", NeonUI.PlayTriangle, Color.white, new Vector2(-21f, 0f), new Vector2(16f, 18f));
            TMP_Text adLabel = AddLabel(textSource, chip, "AD", 19f, Color.white, new Vector2(12f, 0f), new Vector2(50f, 40f));
            adLabel.characterSpacing = 8f;

            // Centre "REVIVE" + chip as one group.
            const float chipGap = 18f;
            float labelWidth = label.GetPreferredValues("REVIVE").x;
            float groupWidth = labelWidth + chipGap + chip.sizeDelta.x;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(labelWidth + 20f, rect.sizeDelta.y);
            labelRect.anchoredPosition = new Vector2(-groupWidth * 0.5f + labelWidth * 0.5f, 0f);
            chip.anchoredPosition = new Vector2(groupWidth * 0.5f - chip.sizeDelta.x * 0.5f, 0f);

            // Revive goes through the ad first, so replace the scene's direct call.
            reviveButton.onClick = new Button.ButtonClickedEvent();
            reviveButton.onClick.AddListener(RequestRevive);
        }
    }

    // Shows Revive only when it can be used, styles Restart to match, and
    // centres the row.
    private void LayoutButtons()
    {
        bool showRevive = reviveButton != null && result.canRevive;
        if (reviveButton != null)
            reviveButton.gameObject.SetActive(showRevive);

        // Restart is the main (pink) button unless Revive is offered.
        if (restartFill != null)
            restartFill.color = showRevive ? OffWhite : Pink;
        if (restartLabel != null)
        {
            restartLabel.color = showRevive ? Ink : Color.white;
            // Dark text on the light pill looks thin in linear colour space.
            restartLabel.fontStyle = showRevive ? FontStyles.Bold : FontStyles.Normal;
        }

        float total = 0f;
        int count = 0;
        foreach (Button button in new[] { menuButton, restartButton, reviveButton })
        {
            if (button == null || !button.gameObject.activeSelf)
                continue;
            total += ((RectTransform)button.transform).sizeDelta.x;
            count++;
        }
        total += ButtonGap * Mathf.Max(0, count - 1);

        float left = -total * 0.5f;
        Button primary = null;
        foreach (Button button in new[] { menuButton, restartButton, reviveButton })
        {
            if (button == null || !button.gameObject.activeSelf)
                continue;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(left + rect.sizeDelta.x * 0.5f, 0f);
            left += rect.sizeDelta.x + ButtonGap;
            primary = button;
        }

        if (primaryGlow != null && primary != null && primary != menuButton)
            primaryGlow.rectTransform.anchoredPosition = ((RectTransform)primary.transform).anchoredPosition;
    }

    private void SetHudHidden(bool hidden)
    {
        foreach (CanvasGroup group in hudGroups)
        {
            if (group == null)
                continue;
            group.alpha = hidden ? 0f : 1f;
            group.blocksRaycasts = !hidden;
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (Button button in new[] { menuButton, restartButton, reviveButton })
        {
            if (button != null)
                button.interactable = interactable;
        }
    }

    // ---------- Animation ----------

    private void Animate()
    {
        if (!built)
            return;

        float appear = Mathf.Clamp01((time - ShowDelay) / FadeDuration);
        float eased = 1f - Mathf.Pow(1f - appear, 3f);
        layoutGroup.alpha = eased;
        // Touches are blocked from the start; the buttons wake once it's visible.
        layoutGroup.interactable = appear >= 0.5f && !reviving;
        title.anchoredPosition = new Vector2(0f, 22f * (1f - eased));

        float count = Mathf.Clamp01((time - ShowDelay - CountDelay) / CountDuration);
        float countEased = 1f - Mathf.Pow(1f - count, 3f);
        scoreText.text = Mathf.RoundToInt(result.score * countEased).ToString();

        if (result.newBest)
        {
            float pop = (time - ShowDelay - CountDelay - CountDuration) / BadgePopDuration;
            float scale = pop <= 0f ? 0f : pop >= 1f ? 1f + 0.04f * Mathf.Sin((time - ShowDelay) * 4.2f) : BackOut(pop);
            badge.localScale = Vector3.one * scale;
        }
        else
        {
            bestText.alpha = count;
        }

        if (primaryGlow != null)
        {
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2.9f);
            primaryGlow.color = WithAlpha(Pink, Mathf.Lerp(0.25f, 0.5f, breathe));
        }
    }

    private static float BackOut(float t)
    {
        const float overshoot = 2.2f;
        t -= 1f;
        return 1f + t * t * ((overshoot + 1f) * t + overshoot);
    }

    // ---------- Helpers ----------

    private static Color StreakColor(int multiplier)
    {
        switch (multiplier)
        {
            case 2: return Cyan;
            case 3: return Amber;
            default: return multiplier >= 4 ? Pink : OffWhite;
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    private static Button FindButton(Transform panel, string name)
    {
        Transform child = panel.Find(name);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private RectTransform AdoptButton(Button button, Vector2 size)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.SetParent(buttonRow, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static TMP_Text StyleLabel(Button button, string text, float fontSize, Color color)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return null;

        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.color = color;
        label.characterSpacing = 14f;
        label.fontFeatures.Clear();
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        Stretch(label.rectTransform);
        label.rectTransform.localScale = Vector3.one;
        return label;
    }

    private static void MakePill(Image image, float height)
    {
        image.type = Image.Type.Sliced;
        // The sprite's corners are 40 units; scale them to a full pill.
        image.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / (height * 0.5f);
    }

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
        Vector2 position, Vector2 size)
    {
        TMP_Text label = Instantiate(textSource, parent, false);
        label.gameObject.name = "Label";
        label.text = text;
        label.richText = true;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.color = color;
        label.characterSpacing = 0f;
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
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

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
