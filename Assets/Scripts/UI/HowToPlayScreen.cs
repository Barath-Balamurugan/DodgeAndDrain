using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// How to play (the TutorialScreen), built in code on top of the existing
// screen: the menu backdrop, four numbered cards (move, drain, shoot, heat)
// each looping a real gameplay clip from Resources/Tutorial, a line about
// waves and power-ups, the existing button (scene wiring kept) as PLAY, and a
// BACK button when it was opened from the main screen. Lives on TutorialScreen;
// the layout sits on the root canvas and shows / hides with it.
public class HowToPlayScreen : MonoBehaviour
{
    private struct Step
    {
        public string title;
        public string caption;
        public string clip;
        public Color accent;
    }

    private static readonly Color Pink = new Color(1f, 0.3f, 0.46f);
    private static readonly Color Cyan = new Color(0.16f, 0.93f, 0.97f);
    private static readonly Color Amber = new Color(1f, 0.7f, 0.28f);
    private static readonly Color OffWhite = new Color(0.955f, 0.955f, 0.955f);
    private static readonly Color CardBody = new Color(0.09f, 0.09f, 0.1f, 0.97f);
    private static readonly Vector2 CardSize = new Vector2(410f, 470f);
    private static readonly Vector2 VideoSize = new Vector2(370f, 256f);
    private const float CardGap = 32f;
    private const float EntranceDuration = 0.45f;

    private bool built;
    private bool showBack;
    private float entranceTime;

    private RectTransform layout;
    private CanvasGroup layoutGroup;
    private RectTransform playButton;
    private RectTransform backButton;
    private readonly List<RectTransform> cards = new List<RectTransform>();
    private readonly List<Vector2> cardRestPositions = new List<Vector2>();
    private readonly List<VideoPlayer> players = new List<VideoPlayer>();
    private readonly List<RawImage> videoImages = new List<RawImage>();

    // Opened from the main screen (BACK returns there) or on first launch (PLAY only).
    public void SetBackVisible(bool visible)
    {
        showBack = visible;
        if (built)
            LayoutButtons();
    }

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
        LayoutButtons();
        foreach (VideoPlayer player in players)
        {
            if (player.clip != null)
                player.Play();
        }
    }

    private void OnDisable()
    {
        if (layout != null)
            layout.gameObject.SetActive(false);

        foreach (VideoPlayer player in players)
        {
            if (player != null)
                player.Stop();
        }
    }

    private void Update()
    {
        entranceTime += Time.unscaledDeltaTime;
        Animate();
    }

    // Editor preview: build and show still frames in place of the videos.
    public void BuildForPreview(bool withBack, Texture[] stills)
    {
        Build();
        showBack = withBack;
        if (layout != null)
            layout.gameObject.SetActive(true);
        LayoutButtons();
        for (int i = 0; i < videoImages.Count && stills != null && i < stills.Length; i++)
        {
            if (stills[i] != null)
                videoImages[i].texture = stills[i];
        }
        entranceTime = EntranceDuration + 1f;
        Animate();
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

        Transform oldButton = panel.Find("Button");
        TMP_Text textSource = oldButton != null ? oldButton.GetComponentInChildren<TMP_Text>(true) : panel.GetComponentInChildren<TMP_Text>(true);
        if (textSource == null)
            return;

        built = true;
        ArenaBackdrop.AddTo(panel);

        // The old tilted polaroids go; the cards replace them.
        foreach (string frame in new[] { "Frame1", "Frame2", "Frame3" })
        {
            Transform old = panel.Find(frame);
            if (old != null)
                old.gameObject.SetActive(false);
        }

        GameObject layoutObject = new GameObject("HowToPlayLayout", typeof(RectTransform), typeof(CanvasGroup));
        layout = layoutObject.GetComponent<RectTransform>();
        layout.SetParent(canvas.rootCanvas.transform, false);
        layout.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        layout.anchorMin = Vector2.zero;
        layout.anchorMax = Vector2.one;
        layout.offsetMin = Vector2.zero;
        layout.offsetMax = Vector2.zero;
        layoutGroup = layoutObject.GetComponent<CanvasGroup>();

        BuildHeader(textSource);
        BuildCards(textSource);
        BuildFooter(textSource);
        BuildButtons(oldButton, textSource);

        layout.gameObject.SetActive(isActiveAndEnabled);
    }

    private void BuildHeader(TMP_Text textSource)
    {
        TMP_Text small = AddLabel(textSource, layout, "HOW TO PLAY", 22f, Pink, new Vector2(0.5f, 1f), new Vector2(0f, -58f),
            new Vector2(900f, 40f), TextAlignmentOptions.Center);
        small.characterSpacing = 12f;
        small.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, WithAlpha(Pink, 0.5f), 0.1f, 0.28f);

        TMP_Text title = AddLabel(textSource, layout, "DRAIN TO SHOOT", 40f, OffWhite, new Vector2(0.5f, 1f), new Vector2(0f, -104f),
            new Vector2(1200f, 60f), TextAlignmentOptions.Center);
        title.characterSpacing = 10f;
        title.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, new Color(1f, 1f, 1f, 0.22f), 0.1f, 0.28f);
    }

    private void BuildCards(TMP_Text textSource)
    {
        int overheatSeconds = 3;
        GameManager gameManager = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gameManager != null)
            overheatSeconds = Mathf.RoundToInt(gameManager.overheatDuration);

        string white = "#FFFFFF", cyan = "#28ECF8", pink = "#FF4C76", amber = "#FFB347";
        Step[] steps =
        {
            new Step { title = "MOVE", clip = "move", accent = OffWhite,
                caption = $"DRAG THE <color={white}>JOYSTICK</color> TO MOVE AND DODGE ENEMIES AND THEIR SHOTS." },
            new Step { title = "DRAIN", clip = "drain", accent = Cyan,
                caption = $"HOLD <color={cyan}>DRAIN</color> WITH WHITE ENERGY IN YOUR RING TO GAIN <color={cyan}>AMMO</color>. YOU MOVE SLOWER." },
            new Step { title = "SHOOT", clip = "shoot", accent = Pink,
                caption = $"HOLD <color={pink}>FIRE</color> TO AUTO-TARGET THE NEAREST ENEMY. EACH SHOT COSTS 1 <color={pink}>AMMO</color>." },
            new Step { title = "HEAT", clip = "heat", accent = Amber,
                caption = $"DRAINING BUILDS <color={amber}>HEAT</color>. MAX IT OUT AND DRAINING LOCKS FOR {overheatSeconds} SECONDS." },
        };

        RectTransform row = AddRect(layout, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(10f, 10f));
        for (int i = 0; i < steps.Length; i++)
        {
            float x = (i - (steps.Length - 1) * 0.5f) * (CardSize.x + CardGap);
            BuildCard(textSource, row, steps[i], i + 1, new Vector2(x, 0f));
        }
    }

    private void BuildCard(TMP_Text textSource, RectTransform parent, Step step, int number, Vector2 position)
    {
        RectTransform card = AddRect(parent, $"{step.title} Card", new Vector2(0.5f, 0.5f), position, CardSize);
        cards.Add(card);
        cardRestPositions.Add(position);

        Image glow = AddImage(card, "Glow", NeonUI.OuterGlow, WithAlpha(step.accent, 0.16f), Vector2.zero, CardSize + Vector2.one * (NeonUI.GlowSpread * 2f));
        glow.type = Image.Type.Sliced;
        Image body = AddImage(card, "Body", NeonUI.RoundedRect, CardBody, Vector2.zero, CardSize);
        body.type = Image.Type.Sliced;
        Image edge = AddImage(card, "Edge", NeonUI.RoundedOutline, WithAlpha(step.accent, 0.8f), Vector2.zero, CardSize);
        edge.type = Image.Type.Sliced;

        float top = CardSize.y * 0.5f;
        float left = -CardSize.x * 0.5f;

        AddImage(card, "Number", NeonUI.Circle, step.accent, new Vector2(left + 46f, top - 44f), new Vector2(42f, 42f));
        TMP_Text numberText = AddLabel(textSource, card, number.ToString(), 22f, new Color(0.09f, 0.09f, 0.1f), new Vector2(0.5f, 0.5f),
            new Vector2(left + 46f, top - 44f), new Vector2(42f, 42f), TextAlignmentOptions.Center);
        numberText.characterSpacing = 0f;

        TMP_Text title = AddLabel(textSource, card, step.title, 26f, OffWhite, new Vector2(0.5f, 0.5f),
            new Vector2(left + 80f + 140f, top - 44f), new Vector2(280f, 44f), TextAlignmentOptions.Left);
        title.characterSpacing = 12f;

        // Real gameplay clip, looping into a render texture.
        Vector2 videoPosition = new Vector2(0f, top - 84f - VideoSize.y * 0.5f);
        RectTransform videoRect = AddRect(card, "Clip", new Vector2(0.5f, 0.5f), videoPosition, VideoSize);
        RawImage video = videoRect.gameObject.AddComponent<RawImage>();
        video.raycastTarget = false;
        video.color = Color.white;
        videoImages.Add(video);

        VideoClip clip = Resources.Load<VideoClip>("Tutorial/" + step.clip);
        if (clip != null)
        {
            RenderTexture target = new RenderTexture(520, 360, 0) { name = $"Tutorial_{step.clip}" };
            target.Create();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
            video.texture = target;

            VideoPlayer player = videoRect.gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.skipOnDrop = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.clip = clip;
            players.Add(player);
        }
        else
        {
            video.color = new Color(0.05f, 0.05f, 0.06f, 1f);
        }

        // Thin accent line under the clip.
        AddImage(card, "ClipLine", null, WithAlpha(step.accent, 0.5f), new Vector2(0f, videoPosition.y - VideoSize.y * 0.5f - 1f), new Vector2(VideoSize.x, 2f));

        TMP_Text caption = AddLabel(textSource, card, step.caption, 18f, new Color(0.78f, 0.78f, 0.78f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, videoPosition.y - VideoSize.y * 0.5f - 18f - 55f), new Vector2(VideoSize.x, 110f), TextAlignmentOptions.TopLeft);
        caption.textWrappingMode = TextWrappingModes.Normal;
        caption.characterSpacing = 3f;
        caption.lineSpacing = 8f;
    }

    private void BuildFooter(TMP_Text textSource)
    {
        TMP_Text footer = AddLabel(textSource, layout,
            "CLEAR A WAVE TO PICK A <color=#FFB347>POWER-UP</color>     /     CHAIN KILLS FOR UP TO <color=#FFB347>x4</color> SCORE",
            19f, new Color(0.62f, 0.62f, 0.62f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1600f, 36f),
            TextAlignmentOptions.Center);
        footer.characterSpacing = 6f;
    }

    // The scene's tutorial button (wired to start the run) becomes PLAY; BACK is new.
    private void BuildButtons(Transform oldButton, TMP_Text textSource)
    {
        if (oldButton != null)
        {
            playButton = (RectTransform)oldButton;
            playButton.SetParent(layout, false);
            playButton.anchorMin = playButton.anchorMax = new Vector2(0.5f, 0f);
            playButton.pivot = new Vector2(0.5f, 0.5f);
            playButton.sizeDelta = new Vector2(330f, 86f);
            playButton.localScale = Vector3.one;

            TMP_Text label = playButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "PLAY";
                label.fontSize = 32f;
                label.characterSpacing = 16f;
                label.fontFeatures.Clear();
                label.color = Color.white;
            }
        }

        backButton = AddRect(layout, "Back", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(230f, 70f));
        Image hit = backButton.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0.001f);
        Image edge = AddImage(backButton, "Edge", NeonUI.RoundedOutline, new Color(1f, 1f, 1f, 0.6f), Vector2.zero, backButton.sizeDelta);
        edge.type = Image.Type.Sliced;
        edge.pixelsPerUnitMultiplier = NeonUI.CardCornerRadius / (backButton.sizeDelta.y * 0.5f);
        TMP_Text backLabel = AddLabel(textSource, backButton, "BACK", 22f, OffWhite, new Vector2(0.5f, 0.5f), Vector2.zero,
            backButton.sizeDelta, TextAlignmentOptions.Center);
        backLabel.characterSpacing = 14f;

        Button button = backButton.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            InitialRun initialRun = FindFirstObjectByType<InitialRun>();
            if (initialRun != null)
                initialRun.BackToMenu();
        });
    }

    private void LayoutButtons()
    {
        const float y = 66f;
        if (backButton != null)
        {
            backButton.gameObject.SetActive(showBack);
            backButton.anchoredPosition = new Vector2(-150f, y);
        }

        if (playButton != null)
            playButton.anchoredPosition = new Vector2(showBack ? 145f : 0f, y);
    }

    // ---------- Animation ----------

    private void Animate()
    {
        if (!built)
            return;

        if (layoutGroup != null)
            layoutGroup.alpha = Ease(Mathf.Clamp01(entranceTime / EntranceDuration));

        for (int i = 0; i < cards.Count; i++)
        {
            float progress = Ease(Mathf.Clamp01((entranceTime - i * 0.06f) / EntranceDuration));
            cards[i].anchoredPosition = cardRestPositions[i] + new Vector2(0f, -30f * (1f - progress));
        }
    }

    private static float Ease(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
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
        Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
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
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return label;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
