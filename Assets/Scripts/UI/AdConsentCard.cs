using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The ad consent card for players in the EU/EEA, UK and Switzerland
// (AdsConsent): a dimmed overlay with a short explanation and two equal
// buttons, personalised or general ads. Shown once before the first play and
// again from AD PRIVACY on the main screen. Built in code on the root canvas,
// sorted above everything else.
public class AdConsentCard : MonoBehaviour
{
    private static readonly Color Pink = new Color(1f, 0.3f, 0.46f);
    private static readonly Color Cyan = new Color(0.28f, 0.96f, 1f);
    private static readonly Color OffWhite = new Color(0.955f, 0.955f, 0.955f);
    private static readonly Color Muted = new Color(0.6f, 0.6f, 0.6f);
    private static readonly Color CardFill = new Color(0.082f, 0.082f, 0.094f, 1f);
    private static readonly Color CardEdge = new Color(0.17f, 0.17f, 0.19f, 1f);
    private static readonly Vector2 CardSize = new Vector2(1120f, 600f);
    private static readonly Vector2 ButtonSize = new Vector2(400f, 70f);
    private const int SortingOrder = 600;
    private const float FadeDuration = 0.25f;

    private static AdConsentCard instance;

    private CanvasGroup group;
    private TMP_Text kicker;
    private Image yesEdge;
    private Image noEdge;
    private Action onAnswered;
    private float time;

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    // firstTime: the before-you-play version; otherwise the AD PRIVACY version,
    // which marks the current answer.
    public static void Show(Canvas rootCanvas, TMP_Text textSource, bool firstTime, Action answered = null, bool instant = false)
    {
        if (instance == null)
        {
            if (rootCanvas == null || textSource == null)
                return;
            instance = Build(rootCanvas, textSource);
        }

        instance.onAnswered = answered;
        instance.kicker.text = firstTime ? "BEFORE YOU PLAY" : "AD PRIVACY";
        bool answeredBefore = !firstTime && AdsConsent.HasAnswered;
        instance.yesEdge.color = answeredBefore && AdsConsent.Granted ? Cyan : WithAlpha(Color.white, 0.6f);
        instance.noEdge.color = answeredBefore && !AdsConsent.Granted ? Cyan : WithAlpha(Color.white, 0.6f);

        instance.transform.SetAsLastSibling();
        instance.gameObject.SetActive(true);
        instance.time = instant ? FadeDuration : 0f;
        instance.group.alpha = instant ? 1f : 0f;
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        group.alpha = Mathf.Clamp01(time / FadeDuration);
    }

    private void Answer(bool granted)
    {
        AdsConsent.SetGranted(granted);
        gameObject.SetActive(false);
        Action answered = onAnswered;
        onAnswered = null;
        answered?.Invoke();
    }

    private static AdConsentCard Build(Canvas rootCanvas, TMP_Text textSource)
    {
        GameObject root = new GameObject("AdConsentCard", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(rootCanvas.transform, false);
        Stretch(rootRect);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        AdConsentCard card = root.AddComponent<AdConsentCard>();
        card.group = root.GetComponent<CanvasGroup>();

        // Dim everything behind; also blocks touches until the player answers.
        Image dim = AddImage(rootRect, "Dim", null, new Color(0.02f, 0.02f, 0.025f, 0.9f), Vector2.zero, Vector2.zero);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        RectTransform body = AddRect(rootRect, "Card", Vector2.zero, CardSize);
        Image fill = AddImage(body, "Fill", NeonUI.RoundedRect, CardFill, Vector2.zero, CardSize);
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / 30f;
        Image edge = AddImage(body, "Edge", NeonUI.RoundedOutline, CardEdge, Vector2.zero, CardSize);
        edge.type = Image.Type.Sliced;
        edge.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / 30f;

        card.kicker = AddLabel(textSource, body, string.Empty, 20f, Pink, new Vector2(0f, 240f), new Vector2(1000f, 34f));
        card.kicker.characterSpacing = 14f;

        TMP_Text title = AddLabel(textSource, body, "ADS AND YOUR DATA", 40f, OffWhite, new Vector2(0f, 186f), new Vector2(1000f, 56f));
        title.characterSpacing = 12f;

        TMP_Text free = AddLabel(textSource, body,
            "DRAIN RUN IS FREE. THE ONLY ADS ARE VIDEOS <color=#F4F4F4>YOU CHOOSE TO WATCH</color> TO REVIVE.",
            20f, Muted, new Vector2(0f, 102f), new Vector2(940f, 70f));
        Wrap(free);

        TMP_Text question = AddLabel(textSource, body,
            "CAN OUR AD PARTNERS USE DATA FROM THIS DEVICE, LIKE YOUR ADVERTISING ID, TO SHOW ADS THAT SUIT YOU?",
            20f, Muted, new Vector2(0f, 14f), new Vector2(940f, 70f));
        Wrap(question);

        // Two equal choices: saying no is as easy as saying yes.
        float buttonX = ButtonSize.x * 0.5f + 14f;
        card.yesEdge = AddChoice(textSource, body, "YES, PERSONALISED", new Vector2(-buttonX, -104f), () => card.Answer(true));
        card.noEdge = AddChoice(textSource, body, "NO, GENERAL ADS", new Vector2(buttonX, -104f), () => card.Answer(false));

        TMP_Text footer = AddLabel(textSource, body,
            "CHANGE THIS ANY TIME IN <color=#48F4FF>AD PRIVACY</color> ON THE MAIN SCREEN",
            16f, new Color(0.45f, 0.45f, 0.48f), new Vector2(0f, -196f), new Vector2(1000f, 30f));
        footer.characterSpacing = 6f;

        if (!string.IsNullOrEmpty(AdsConfig.PrivacyPolicyUrl))
        {
            TMP_Text policy = AddLabel(textSource, body, "PRIVACY POLICY", 16f, Cyan, new Vector2(0f, -240f), new Vector2(260f, 36f));
            policy.characterSpacing = 6f;
            policy.raycastTarget = true;
            Button link = policy.gameObject.AddComponent<Button>();
            link.transition = Selectable.Transition.None;
            link.onClick.AddListener(() => Application.OpenURL(AdsConfig.PrivacyPolicyUrl));
        }

        root.SetActive(false);
        return card;
    }

    // Outline pill button; returns its edge so the current answer can be marked.
    private static Image AddChoice(TMP_Text textSource, RectTransform parent, string text, Vector2 position, Action onClick)
    {
        RectTransform rect = AddRect(parent, text, position, ButtonSize);
        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0.001f);

        Image edge = AddImage(rect, "Edge", NeonUI.RoundedOutline, WithAlpha(Color.white, 0.6f), Vector2.zero, ButtonSize);
        edge.type = Image.Type.Sliced;
        edge.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / (ButtonSize.y * 0.5f);

        TMP_Text label = AddLabel(textSource, rect, text, 21f, OffWhite, Vector2.zero, ButtonSize);
        label.characterSpacing = 10f;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = edge;
        button.onClick.AddListener(() => onClick());
        return edge;
    }

    private static void Wrap(TMP_Text label)
    {
        label.textWrappingMode = TextWrappingModes.Normal;
        label.characterSpacing = 4f;
        label.lineSpacing = 10f;
    }

    private static RectTransform AddRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        RectTransform rect = element.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
    {
        Image image = AddRect(parent, name, position, size).gameObject.AddComponent<Image>();
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
        label.fontFeatures.Clear();
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return label;
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
