using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// What a power-up card shows. accent is the card's one colour, taken from the
// game's own palette (pink = shooting, cyan = drain / heat, white = movement).
public struct NeonCardSpec
{
    public string title;
    public bool rare;
    public Color accent;

    // Commons: big stat ("+25%") and its label ("FIRE RATE").
    public string stat;
    public string statLabel;

    // Rares: one-line effect instead of a stat.
    public string description;

    // Bottom line, e.g. "LV 1  >  LV 2" or "NEW ABILITY" (TMP rich text allowed).
    public string footer;

    // Draws the ability symbol, in a 142 x 142 space, onto the icon disc.
    public Action<RectTransform, Color> drawGlyph;
}

// A power-up card in the game's own style with a soft glow: charcoal body,
// a gently glowing edge in the card's colour, a flat icon disc (like the fire
// and drain buttons) with two slow light trails, and a softly glowing stat.
// Rares get a stronger glow, a "RARE" tag and a light sweep. Built entirely in
// code from NeonUI's generated sprites, and animates itself: staggered
// entrance, the rare sweep, and the chosen / dimmed states after a tap.
public class NeonPowerUpCard : MonoBehaviour
{
    public static readonly Vector2 CardSize = new Vector2(360f, 560f);

    private const float EntranceDuration = 0.32f;
    private const float EntranceRise = 40f;
    private const float StateLerpSpeed = 14f;
    private const float SweepCycle = 3.2f;
    private const float TrailDegreesPerSecond = 40f;

    // Charcoal, one step lighter than the arena.
    private static readonly Color BodyColor = new Color(0.09f, 0.09f, 0.1f, 0.97f);

    private enum CardState { Idle, Chosen, Dimmed }

    private RectTransform cardRect;
    private CanvasGroup canvasGroup;
    private Image outerGlow;
    private RectTransform discGlow;
    private RectTransform trails;
    private RectTransform sweep;
    private Vector2 restPosition;
    private float entranceDelay;
    private float age;
    private float baseGlowAlpha;
    private CardState state = CardState.Idle;
    private float shownAlpha = 1f;
    private float shownScale = 1f;
    private float shownGlowBoost;

    public Button Button { get; private set; }

    public void SetChosen() { state = CardState.Chosen; Button.interactable = false; }
    public void SetDimmed() { state = CardState.Dimmed; Button.interactable = false; }

    // Jump to the settled look (used by the editor preview renderer).
    public void SkipEntranceAnimation()
    {
        age = entranceDelay + EntranceDuration;
        ApplyAnimation();
    }

    public static NeonPowerUpCard Create(Transform parent, TMP_Text textSource, NeonCardSpec spec, Vector2 position, float entranceDelay)
    {
        GameObject root = new GameObject($"{spec.title} Card", typeof(RectTransform), typeof(CanvasGroup), typeof(NeonPowerUpCard));
        root.transform.SetParent(parent, false);

        NeonPowerUpCard card = root.GetComponent<NeonPowerUpCard>();
        card.cardRect = root.GetComponent<RectTransform>();
        card.canvasGroup = root.GetComponent<CanvasGroup>();
        card.restPosition = position;
        card.entranceDelay = entranceDelay;
        card.Build(textSource, spec);
        card.ApplyAnimation();
        return card;
    }

    private void Build(TMP_Text textSource, NeonCardSpec spec)
    {
        SetRect(cardRect, restPosition, CardSize);
        canvasGroup.alpha = 0f;

        Color accent = spec.accent;
        Color tint = Color.Lerp(accent, Color.white, 0.45f);
        Material fontMaterial = textSource.fontSharedMaterial;
        Transform root = cardRect;

        // Soft glow around the card (stronger on rares), then the charcoal body
        // with a faint inner light behind the icon. White glows read brighter
        // than coloured ones, so they're scaled down to look equally soft.
        Color.RGBToHSV(accent, out _, out float saturation, out _);
        float glowScale = saturation < 0.2f ? 0.65f : 1f;
        baseGlowAlpha = (spec.rare ? 0.4f : 0.2f) * glowScale;
        outerGlow = AddImage(root, "OuterGlow", NeonUI.OuterGlow, WithAlpha(accent, baseGlowAlpha),
            Vector2.zero, CardSize + Vector2.one * (NeonUI.GlowSpread * 2f), sliced: true);

        Image body = AddImage(root, "Body", NeonUI.RoundedRect, BodyColor, Vector2.zero, CardSize, sliced: true);
        body.raycastTarget = true;
        AddImage(root, "InnerLight", NeonUI.SoftDot, WithAlpha(accent, (spec.rare ? 0.2f : 0.15f) * glowScale), new Vector2(0f, 110f), new Vector2(350f, 350f));

        if (spec.rare)
        {
            RectTransform sweepMask = AddRect(root, "SweepMask", Vector2.zero, CardSize - new Vector2(20f, 20f));
            sweepMask.gameObject.AddComponent<RectMask2D>();
            Image sweepImage = AddImage(sweepMask, "Sweep", NeonUI.SoftDot, new Color(1f, 1f, 1f, 0.13f), new Vector2(-300f, 0f), new Vector2(80f, 820f));
            sweep = sweepImage.rectTransform;
            sweep.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        AddImage(root, "Edge", NeonUI.RoundedOutline, WithAlpha(accent, spec.rare ? 1f : 0.75f), Vector2.zero, CardSize, sliced: true);

        // Rarity tag.
        AddLabel(textSource, root, spec.rare ? "RARE" : "COMMON", 20f, tint, new Vector2(0f, 232f), new Vector2(300f, 36f),
            NeonUI.GlowTextMaterial(fontMaterial, WithAlpha(accent, 0.4f), 0.1f, 0.25f));
        if (spec.rare)
        {
            AddDiamond(root, new Vector2(-66f, 232f), 11f, accent);
            AddDiamond(root, new Vector2(66f, 232f), 11f, accent);
        }

        BuildIcon(root, spec);

        AddLabel(textSource, root, spec.title, 30f, new Color(0.95f, 0.95f, 0.95f, 1f), new Vector2(0f, -22f), new Vector2(330f, 70f),
            NeonUI.GlowTextMaterial(fontMaterial, WithAlpha(accent, 0.45f), 0.12f, 0.26f));

        if (spec.rare)
        {
            AddLabel(textSource, root, spec.description, 21f, new Color(0.9f, 0.9f, 0.9f, 1f),
                new Vector2(0f, -108f), new Vector2(300f, 90f), null);
        }
        else
        {
            AddImage(root, "StatGlow", NeonUI.SoftDot, WithAlpha(accent, 0.18f), new Vector2(0f, -86f), new Vector2(240f, 90f));
            AddLabel(textSource, root, spec.stat, 60f, Color.white, new Vector2(0f, -86f), new Vector2(320f, 80f),
                NeonUI.GlowTextMaterial(fontMaterial, WithAlpha(accent, 0.7f), 0.16f, 0.3f));
            AddLabel(textSource, root, spec.statLabel, 20f, tint, new Vector2(0f, -140f), new Vector2(320f, 36f), null);
        }

        AddLabel(textSource, root, spec.footer, 20f, spec.rare ? tint : new Color(0.95f, 0.95f, 0.95f, 1f),
            new Vector2(0f, -232f), new Vector2(320f, 36f), null);

        Button = cardRect.gameObject.AddComponent<Button>();
        Button.targetGraphic = body;
        Button.transition = Selectable.Transition.None;
        Button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    // Flat disc in the card colour with a dark symbol (like the fire / drain
    // buttons), a soft glow behind it and two thin light trails circling it.
    private void BuildIcon(Transform root, NeonCardSpec spec)
    {
        RectTransform icon = AddRect(root, "Icon", new Vector2(0f, 104f), new Vector2(210f, 210f));

        discGlow = AddImage(icon, "DiscGlow", NeonUI.SoftDot, WithAlpha(spec.accent, 0.45f), Vector2.zero, new Vector2(210f, 210f)).rectTransform;

        trails = AddRect(icon, "LightTrails", Vector2.zero, new Vector2(210f, 210f));
        AddImage(trails, "TrailA", NeonUI.Arc, spec.accent, Vector2.zero, new Vector2(210f, 210f), rotation: 35f);
        AddImage(trails, "TrailB", NeonUI.Arc, spec.rare ? new Color(1f, 1f, 1f, 0.8f) : spec.accent,
            Vector2.zero, new Vector2(210f, 210f), rotation: 215f);
        if (spec.rare)
            AddImage(trails, "TrailC", NeonUI.Arc, WithAlpha(spec.accent, 0.6f), Vector2.zero, new Vector2(232f, 232f), rotation: 120f);

        AddImage(icon, "Disc", NeonUI.Circle, spec.accent, Vector2.zero, new Vector2(122f, 122f));

        if (spec.drawGlyph != null)
        {
            RectTransform glyph = AddRect(icon, "Glyph", Vector2.zero, new Vector2(142f, 142f));
            glyph.localScale = Vector3.one * 0.6f;
            spec.drawGlyph(glyph, Color.Lerp(spec.accent, Color.black, 0.78f));
        }
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        ApplyAnimation();
    }

    private void ApplyAnimation()
    {
        float deltaTime = Time.unscaledDeltaTime;
        float targetAlpha = state == CardState.Dimmed ? 0.22f : 1f;
        float targetScale = state == CardState.Chosen ? 1.07f : state == CardState.Dimmed ? 0.93f : 1f;
        float targetBoost = state == CardState.Chosen ? 1f : 0f;
        float blend = 1f - Mathf.Exp(-StateLerpSpeed * deltaTime);
        shownAlpha = Mathf.Lerp(shownAlpha, targetAlpha, blend);
        shownScale = Mathf.Lerp(shownScale, targetScale, blend);
        shownGlowBoost = Mathf.Lerp(shownGlowBoost, targetBoost, blend);

        float entrance = Mathf.Clamp01((age - entranceDelay) / EntranceDuration);
        float eased = 1f - Mathf.Pow(1f - entrance, 3f);

        canvasGroup.alpha = eased * shownAlpha;
        cardRect.anchoredPosition = restPosition + new Vector2(0f, -EntranceRise * (1f - eased) + 10f * shownGlowBoost);
        cardRect.localScale = Vector3.one * shownScale;

        if (outerGlow != null)
            outerGlow.color = WithAlpha(outerGlow.color, Mathf.Lerp(baseGlowAlpha, 0.85f, shownGlowBoost));

        if (trails != null)
            trails.localRotation = Quaternion.Euler(0f, 0f, -age * TrailDegreesPerSecond);
        if (discGlow != null)
            discGlow.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(age * 3f) + 0.15f * shownGlowBoost);

        if (sweep != null)
        {
            float phase = (age % SweepCycle) / SweepCycle;
            float x = phase < 0.5f ? Mathf.Lerp(-300f, 300f, phase / 0.5f) : 300f;
            sweep.anchoredPosition = new Vector2(x, 0f);
        }
    }

    // ---------- Building helpers ----------

    private static RectTransform AddRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        element.transform.SetParent(parent, false);
        RectTransform rect = element.GetComponent<RectTransform>();
        SetRect(rect, position, size);
        return rect;
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size,
        bool sliced = false, float rotation = 0f)
    {
        RectTransform rect = AddRect(parent, name, position, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AddDiamond(Transform parent, Vector2 position, float size, Color color)
    {
        RectTransform rect = AddRect(parent, "Diamond", position, new Vector2(size, size));
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void AddLabel(TMP_Text textSource, Transform parent, string text, float fontSize, Color color,
        Vector2 position, Vector2 size, Material material)
    {
        if (string.IsNullOrEmpty(text) || textSource == null)
            return;

        TMP_Text label = Instantiate(textSource, parent, false);
        label.gameObject.name = "Label";
        label.text = text;
        label.richText = true;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = color;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        if (material != null)
            label.fontSharedMaterial = material;

        RectTransform rect = label.rectTransform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        SetRect(rect, position, size);
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
