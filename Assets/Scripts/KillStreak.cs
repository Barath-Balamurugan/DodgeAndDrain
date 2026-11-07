using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Kill-streak score multiplier. Kills that land within streakWindow seconds of
// each other build a streak, and longer streaks multiply the points each enemy
// kill is worth (x2, x3, x4). The streak breaks after a gap, on death, revive or
// restart. Creates itself at startup and builds its HUD label under the ammo
// counter, so nothing needs wiring in the scene.
public class KillStreak : MonoBehaviour
{
    public static KillStreak Instance { get; private set; }

    [Header("Streak Rules")]
    [Tooltip("Seconds after a kill before the streak breaks.")]
    [SerializeField, Min(0.1f)] private float streakWindow = 2.5f;
    [Tooltip("Kills in a row needed for x2, x3, x4, ... (ascending).")]
    [SerializeField] private int[] multiplierThresholds = { 3, 8, 15 };

    [Header("HUD")]
    [Tooltip("Offset from the ammo counter (same spacing as Score / Energy / Ammo).")]
    [SerializeField] private Vector2 offsetFromAmmoText = new Vector2(0f, -50f);
    [SerializeField, Min(1f)] private float timerBarWidth = 170f;
    [SerializeField, Min(1f)] private float timerBarHeight = 5f;
    [SerializeField, Min(0.01f)] private float tierUpPulseDuration = 0.28f;
    [SerializeField, Min(0f)] private float tierUpPulseScale = 0.35f;
    [SerializeField] private Color[] tierColors =
    {
        new Color(0.45f, 0.95f, 1f, 1f),   // x2  cyan
        new Color(1f, 0.78f, 0.3f, 1f),    // x3  amber
        new Color(1f, 0.35f, 0.55f, 1f)    // x4+ hot pink
    };

    public int Count { get; private set; }
    public int Multiplier { get; private set; } = 1;

    private float timer;
    private float pulseTimer;
    private TMP_Text label;
    private RectTransform labelRect;
    private Vector3 labelRestScale = Vector3.one;
    private Image timerBar;
    private RectTransform timerBarRect;
    private int shownMultiplier = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStartup()
    {
        if (Instance == null)
            new GameObject("KillStreak").AddComponent<KillStreak>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Call once per enemy kill. Returns the multiplier to apply to that kill.
    public int RegisterKill()
    {
        Count++;
        timer = streakWindow;

        int newMultiplier = MultiplierFor(Count);
        if (newMultiplier > Multiplier)
        {
            pulseTimer = tierUpPulseDuration;
            if (GameFeedback.Instance != null)
                GameFeedback.Instance.PlayStreakUp(newMultiplier);
        }

        Multiplier = newMultiplier;
        return Multiplier;
    }

    public void ResetStreak()
    {
        Count = 0;
        Multiplier = 1;
        timer = 0f;
        pulseTimer = 0f;
    }

    private int MultiplierFor(int kills)
    {
        int multiplier = 1;
        if (multiplierThresholds == null)
            return multiplier;

        for (int i = 0; i < multiplierThresholds.Length; i++)
        {
            if (kills >= multiplierThresholds[i])
                multiplier = i + 2;
        }

        return multiplier;
    }

    private void Update()
    {
        if (Count > 0)
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.IsGameOver)
            {
                ResetStreak();
            }
            else
            {
                // Scaled time: pausing freezes the streak, and so does hit-stop.
                timer -= Time.deltaTime;
                if (timer <= 0f)
                    ResetStreak();
            }
        }

        UpdateHud();
    }

    private void UpdateHud()
    {
        if (!EnsureHud())
            return;

        bool visible = Multiplier >= 2;
        if (label.enabled != visible)
        {
            label.enabled = visible;
            timerBar.enabled = visible;
        }

        if (!visible)
        {
            shownMultiplier = -1;
            labelRect.localScale = labelRestScale;
            return;
        }

        Color color = tierColors != null && tierColors.Length > 0
            ? tierColors[Mathf.Clamp(Multiplier - 2, 0, tierColors.Length - 1)]
            : Color.white;

        if (shownMultiplier != Multiplier)
        {
            shownMultiplier = Multiplier;
            label.text = $"STREAK x{Multiplier}";
            label.color = color;
            timerBar.color = new Color(color.r, color.g, color.b, 0.85f);
        }

        float remaining = streakWindow > 0f ? Mathf.Clamp01(timer / streakWindow) : 0f;
        timerBarRect.sizeDelta = new Vector2(timerBarWidth * remaining, timerBarHeight);

        // Unscaled so the pop still plays during the kill hit-stop.
        pulseTimer = Mathf.Max(0f, pulseTimer - Time.unscaledDeltaTime);
        float pulse = pulseTimer > 0f ? Mathf.Sin((1f - pulseTimer / tierUpPulseDuration) * Mathf.PI) : 0f;
        labelRect.localScale = labelRestScale * (1f + tierUpPulseScale * pulse);
    }

    // Builds the label and timer bar next to the HUD's ammo counter the first
    // time they are needed (and again if the scene was reloaded).
    private bool EnsureHud()
    {
        if (label != null && timerBar != null)
            return true;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return false;

        PlayerAmmo playerAmmo = gameManager.player != null ? gameManager.player.GetComponent<PlayerAmmo>() : null;
        TMP_Text anchorText = playerAmmo != null && playerAmmo.ammoText != null
            ? playerAmmo.ammoText
            : gameManager.scoreText;
        if (anchorText == null)
            return false;

        RectTransform anchorRect = anchorText.rectTransform;
        Vector2 offset = anchorText == gameManager.scoreText ? offsetFromAmmoText * 3f : offsetFromAmmoText;

        label = Instantiate(anchorText, anchorRect.parent, false);
        label.gameObject.name = "KillStreakText";
        label.text = string.Empty;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.enabled = false;

        // The copied ammo text may carry PlayerAmmo-driven state; only keep the look.
        labelRect = label.rectTransform;
        labelRect.anchorMin = anchorRect.anchorMin;
        labelRect.anchorMax = anchorRect.anchorMax;
        labelRect.pivot = anchorRect.pivot;
        labelRect.sizeDelta = anchorRect.sizeDelta + new Vector2(80f, 0f);
        labelRect.anchoredPosition = anchorRect.anchoredPosition + offset;
        labelRect.localScale = Vector3.one;
        labelRestScale = Vector3.one;

        GameObject barObject = new GameObject("KillStreakTimer", typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(anchorRect.parent, false);
        timerBar = barObject.GetComponent<Image>();
        timerBar.raycastTarget = false;
        timerBar.enabled = false;

        timerBarRect = barObject.GetComponent<RectTransform>();
        timerBarRect.anchorMin = anchorRect.anchorMin;
        timerBarRect.anchorMax = anchorRect.anchorMax;
        timerBarRect.pivot = new Vector2(0f, 1f);
        timerBarRect.anchoredPosition = labelRect.anchoredPosition + new Vector2(0f, -anchorRect.sizeDelta.y + 4f);
        timerBarRect.sizeDelta = new Vector2(timerBarWidth, timerBarHeight);

        shownMultiplier = -1;
        return true;
    }
}
