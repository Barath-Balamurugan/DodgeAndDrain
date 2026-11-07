using UnityEngine;
using UnityEngine.UI;

// The menu screens' background: the arena's charcoal with two layers of
// randomly scattered dots drifting at different speeds (small, faint ones
// behind, bigger, brighter ones in front) and a faint pink glow. Add it to a
// full-screen panel; it replaces the panel's own image.
public class ArenaBackdrop : MonoBehaviour
{
    [SerializeField] private Color backgroundColor = new Color(0.055f, 0.055f, 0.06f, 1f);
    [SerializeField] private Color glowColor = new Color(0.55f, 0.16f, 0.36f, 0.3f);
    [Tooltip("Back layer: small, faint dots drifting slowly.")]
    [SerializeField] private Color farDotColor = new Color(0.12f, 0.12f, 0.135f, 1f);
    [Tooltip("Front layer: bigger, slightly brighter dots drifting faster.")]
    [SerializeField] private Color nearDotColor = new Color(0.157f, 0.157f, 0.172f, 1f);
    [Tooltip("Drift speed of the back / front dot layers, in UI units per second.")]
    [SerializeField] private Vector2 dotDriftSpeed = new Vector2(12f, 32f);

    private RawImage farDots;
    private RawImage nearDots;
    private Vector2 tiles;
    private float time;

    // Adds the backdrop to a panel (once) and returns it.
    public static ArenaBackdrop AddTo(Transform panel)
    {
        ArenaBackdrop backdrop = panel.GetComponent<ArenaBackdrop>();
        if (backdrop == null)
        {
            backdrop = panel.gameObject.AddComponent<ArenaBackdrop>();
            backdrop.Build();
        }

        return backdrop;
    }

    private void Build()
    {
        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = null;
            panelImage.color = backgroundColor;
        }

        tiles = ((RectTransform)transform).rect.size / NeonUI.ScatterTileSize;
        farDots = AddDotLayer("FarDots", NeonUI.FarDots, farDotColor, 0);
        nearDots = AddDotLayer("NearDots", NeonUI.NearDots, nearDotColor, 1);

        GameObject glowObject = new GameObject("PinkGlow", typeof(RectTransform), typeof(Image));
        RectTransform glowRect = glowObject.GetComponent<RectTransform>();
        glowRect.SetParent(transform, false);
        glowRect.SetSiblingIndex(2);
        glowRect.anchoredPosition = new Vector2(0f, -40f);
        glowRect.sizeDelta = new Vector2(1700f, 1000f);
        Image glow = glowObject.GetComponent<Image>();
        glow.sprite = NeonUI.SoftDot;
        glow.color = glowColor;
        glow.raycastTarget = false;
    }

    private RawImage AddDotLayer(string name, Texture texture, Color color, int siblingIndex)
    {
        GameObject layerObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.SetSiblingIndex(siblingIndex);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage layer = layerObject.GetComponent<RawImage>();
        layer.texture = texture;
        layer.color = color;
        layer.raycastTarget = false;
        layer.uvRect = new Rect(0f, 0f, tiles.x, tiles.y);
        return layer;
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        float tile = NeonUI.ScatterTileSize;
        if (farDots != null)
            farDots.uvRect = new Rect(time * dotDriftSpeed.x / tile, time * dotDriftSpeed.x * 0.5f / tile, tiles.x, tiles.y);
        if (nearDots != null)
            nearDots.uvRect = new Rect(time * dotDriftSpeed.y / tile, time * dotDriftSpeed.y * 0.5f / tile, tiles.x, tiles.y);
    }
}
