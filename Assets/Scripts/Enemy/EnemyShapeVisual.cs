using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class EnemyShapeVisual : MonoBehaviour
{
    [SerializeField] private Sprite shapeSprite;
    [SerializeField] private bool useTriangle;
    [SerializeField] private float rotationDegrees;

    public SpriteRenderer Renderer { get; private set; }

    private static Sprite triangleSprite;

    private void Awake()
    {
        SpriteRenderer originalRenderer = GetComponent<SpriteRenderer>();
        Sprite visualSprite = useTriangle ? GetTriangleSprite() : shapeSprite;
        if (visualSprite == null || originalRenderer.sprite == null)
        {
            Renderer = originalRenderer;
            return;
        }

        GameObject shapeObject = new GameObject("ShapeVisual");
        Transform shapeTransform = shapeObject.transform;
        shapeTransform.SetParent(transform, false);
        shapeTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

        Renderer = shapeObject.AddComponent<SpriteRenderer>();
        Renderer.sprite = visualSprite;
        Renderer.color = originalRenderer.color;
        Renderer.sharedMaterial = originalRenderer.sharedMaterial;
        Renderer.sortingLayerID = originalRenderer.sortingLayerID;
        Renderer.sortingOrder = originalRenderer.sortingOrder;

        // Match the original sprite's visible size without scaling the root
        // object, so the enemy's existing collider and gameplay stay unchanged.
        Vector2 originalSize = originalRenderer.sprite.bounds.size;
        Vector2 shapeSize = visualSprite.bounds.size;
        shapeTransform.localScale = new Vector3(
            shapeSize.x > 0f ? originalSize.x / shapeSize.x : 1f,
            shapeSize.y > 0f ? originalSize.y / shapeSize.y : 1f,
            1f
        );

        originalRenderer.enabled = false;
    }

    private static Sprite GetTriangleSprite()
    {
        if (triangleSprite != null)
            return triangleSprite;

        const int size = 128;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float height = (y + 0.5f) / size;
            float halfWidth = (1f - height) * 0.5f;

            for (int x = 0; x < size; x++)
            {
                float horizontalPosition = (x + 0.5f) / size - 0.5f;
                if (Mathf.Abs(horizontalPosition) <= halfWidth)
                    pixels[y * size + x] = Color.white;
            }
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DasherTriangle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply();

        triangleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        triangleSprite.name = "DasherTriangle";
        triangleSprite.hideFlags = HideFlags.HideAndDontSave;
        return triangleSprite;
    }
}
