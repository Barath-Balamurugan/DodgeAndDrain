using System.Collections.Generic;
using UnityEngine;

// Small arrows at the edge of the screen pointing at enemies the camera can't
// see, so nothing reaches the player unannounced. Each arrow uses its enemy's
// colour and grows and brightens as the enemy gets closer.
public class OffscreenEnemyIndicators : MonoBehaviour
{
    [SerializeField] private bool showIndicators = true;
    [Tooltip("How far in from the screen edge the arrows sit, in world units.")]
    [SerializeField, Min(0f)] private float edgeInset = 1.3f;
    [Tooltip("Arrow size for a far enemy (x) and one just off screen (y), in world units.")]
    [SerializeField] private Vector2 arrowSizeRange = new Vector2(0.8f, 1.4f);
    [Tooltip("Enemies this far beyond the screen edge (or more) get the smallest, faintest arrow.")]
    [SerializeField, Min(0.1f)] private float farDistance = 25f;

    private readonly List<SpriteRenderer> arrows = new List<SpriteRenderer>();
    private Sprite arrowSprite;
    private Material arrowMaterial;
    private int sortingLayer;
    private int sortingOrder;

    private void LateUpdate()
    {
        int used = 0;
        Camera camera = Camera.main;
        GameManager gameManager = GameManager.Instance;

        if (showIndicators && camera != null && camera.orthographic && gameManager != null && !gameManager.IsGameOver && EnsureResources())
        {
            Vector2 cameraPosition = camera.transform.position;
            Vector2 halfView = new Vector2(camera.orthographicSize * camera.aspect, camera.orthographicSize);
            Vector2 inner = halfView - Vector2.one * edgeInset;

            foreach (EnemyBase enemy in EnemyBase.Active)
            {
                if (enemy == null)
                    continue;

                Vector2 delta = (Vector2)enemy.transform.position - cameraPosition;
                if (Mathf.Abs(delta.x) <= halfView.x && Mathf.Abs(delta.y) <= halfView.y)
                    continue;

                // Slide along the line from the screen centre to the enemy until
                // it meets the inset screen edge.
                float scale = Mathf.Min(
                    inner.x / Mathf.Max(Mathf.Abs(delta.x), 0.0001f),
                    inner.y / Mathf.Max(Mathf.Abs(delta.y), 0.0001f));
                Vector2 edgePoint = cameraPosition + delta * scale;
                float beyond = delta.magnitude * (1f - scale);
                float closeness = 1f - Mathf.Clamp01(beyond / farDistance);

                SpriteRenderer arrow = GetArrow(used++);
                arrow.transform.SetPositionAndRotation(
                    new Vector3(edgePoint.x, edgePoint.y, 0f),
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
                arrow.transform.localScale = Vector3.one * Mathf.Lerp(arrowSizeRange.x, arrowSizeRange.y, closeness);

                Color color = enemy.IndicatorColor;
                color.a = Mathf.Lerp(0.5f, 0.95f, closeness);
                arrow.color = color;
                arrow.enabled = true;
            }
        }

        for (int i = used; i < arrows.Count; i++)
            arrows[i].enabled = false;
    }

    private SpriteRenderer GetArrow(int index)
    {
        if (index < arrows.Count)
            return arrows[index];

        GameObject arrowObject = new GameObject("OffscreenArrow");
        arrowObject.transform.SetParent(transform, false);
        SpriteRenderer arrow = arrowObject.AddComponent<SpriteRenderer>();
        arrow.sprite = arrowSprite;
        arrow.sharedMaterial = arrowMaterial;
        arrow.sortingLayerID = sortingLayer;
        arrow.sortingOrder = sortingOrder;
        arrows.Add(arrow);
        return arrow;
    }

    // Material and sorting come from the player's sprite so arrows draw over
    // gameplay; the arrow sprite (a rounded triangle pointing right, 1 unit) is
    // generated once.
    private bool EnsureResources()
    {
        if (arrowSprite != null && arrowMaterial != null)
            return true;

        GameManager gameManager = GameManager.Instance;
        PlayerMovement2D player = gameManager != null ? gameManager.player : null;
        SpriteRenderer reference = player != null
            ? (player.VisualRenderer != null ? player.VisualRenderer : player.GetComponent<SpriteRenderer>())
            : null;
        if (reference == null || reference.sharedMaterial == null)
            return false;

        arrowMaterial = reference.sharedMaterial;
        sortingLayer = reference.sortingLayerID;
        sortingOrder = reference.sortingOrder + 10;
        arrowSprite = CreateArrowSprite(64);
        return true;
    }

    private static Sprite CreateArrowSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "OffscreenArrow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        // A slim dart pointing +x with a notch in the back, so its direction is
        // unmistakable at any rotation (a plain near-equilateral triangle looks
        // the same pointing several ways). Built from two triangles that share
        // the tip and the notch.
        Vector2 tip = new Vector2(size * 0.95f, size * 0.5f);
        Vector2 backTop = new Vector2(size * 0.1f, size * 0.84f);
        Vector2 notch = new Vector2(size * 0.36f, size * 0.5f);
        Vector2 backBottom = new Vector2(size * 0.1f, size * 0.16f);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float upper = TriangleDistance(p, notch, tip, backTop);
                float lower = TriangleDistance(p, backBottom, tip, notch);
                float distance = Mathf.Min(upper, lower);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // Approximate signed distance to a counter-clockwise triangle (negative inside).
    private static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        return Mathf.Max(EdgeDistance(p, a, b), Mathf.Max(EdgeDistance(p, b, c), EdgeDistance(p, c, a)));
    }

    // Signed distance from p to the edge a->b (positive outside a counter-clockwise triangle).
    private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        Vector2 normal = new Vector2(edge.y, -edge.x).normalized;
        return Vector2.Dot(p - a, normal);
    }
}
