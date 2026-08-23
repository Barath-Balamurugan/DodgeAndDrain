using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class EnemyShapeVisual : MonoBehaviour
{
    [SerializeField] private Sprite shapeSprite;
    [SerializeField] private float rotationDegrees;

    public SpriteRenderer Renderer { get; private set; }

    private void Awake()
    {
        SpriteRenderer originalRenderer = GetComponent<SpriteRenderer>();
        if (shapeSprite == null || originalRenderer.sprite == null)
        {
            Renderer = originalRenderer;
            return;
        }

        GameObject shapeObject = new GameObject("ShapeVisual");
        Transform shapeTransform = shapeObject.transform;
        shapeTransform.SetParent(transform, false);
        shapeTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

        Renderer = shapeObject.AddComponent<SpriteRenderer>();
        Renderer.sprite = shapeSprite;
        Renderer.color = originalRenderer.color;
        Renderer.sharedMaterial = originalRenderer.sharedMaterial;
        Renderer.sortingLayerID = originalRenderer.sortingLayerID;
        Renderer.sortingOrder = originalRenderer.sortingOrder;

        // Match the original sprite's visible size without scaling the root
        // object, so the enemy's existing collider and gameplay stay unchanged.
        Vector2 originalSize = originalRenderer.sprite.bounds.size;
        Vector2 shapeSize = shapeSprite.bounds.size;
        shapeTransform.localScale = new Vector3(
            shapeSize.x > 0f ? originalSize.x / shapeSize.x : 1f,
            shapeSize.y > 0f ? originalSize.y / shapeSize.y : 1f,
            1f
        );

        originalRenderer.enabled = false;
    }
}
