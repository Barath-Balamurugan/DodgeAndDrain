using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public Joystick joystick;
    public float moveSpeed = 5f;
    public float reducedMoveSpeed = 2f;

    [Header("Visual Scale")]
    [SerializeField, Min(0.1f)] private float visualScaleMultiplier = 1.75f;

    public bool isDraining;
    public SpriteRenderer VisualRenderer { get; private set; }

    private Rigidbody2D rb;

    private Vector2 minBounds;
    private Vector2 maxBounds;
    private float halfWidth;
    private float halfHeight;
    private float baseMoveSpeed;
    private float baseReducedMoveSpeed;
    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private void Awake()
    {
        baseMoveSpeed = moveSpeed;
        baseReducedMoveSpeed = reducedMoveSpeed;
        startingPosition = transform.position;
        startingRotation = transform.rotation;
        CreateScaledPlayerVisual();

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        // The camera follows the player every frame, but physics moves it at a
        // fixed rate; interpolation keeps the player smooth on screen.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        UpdateCameraBounds();
        CachePlayerExtents();
    }

    // The player's movement limits: the arena walls when there's an Arena (the
    // camera follows), otherwise the camera view (fixed play field).
    private void UpdateCameraBounds()
    {
        if (Arena.Instance != null)
        {
            minBounds = Arena.Instance.Min;
            maxBounds = Arena.Instance.Max;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        // For an orthographic camera, z doesn't matter much, but keep it consistent
        float z = Mathf.Abs(cam.transform.position.z - transform.position.z);

        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, z));
        Vector3 topRight   = cam.ViewportToWorldPoint(new Vector3(1f, 1f, z));

        minBounds = bottomLeft;
        maxBounds = topRight;
    }

    private void CachePlayerExtents()
    {
        // Keep the enlarged visual inside the camera bounds.
        SpriteRenderer sr = VisualRenderer != null ? VisualRenderer : GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            halfWidth  = sr.bounds.extents.x;
            halfHeight = sr.bounds.extents.y;
            return;
        }

        // Or use Collider2D if you have one
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            halfWidth  = col.bounds.extents.x;
            halfHeight = col.bounds.extents.y;
        }
    }

    private void FixedUpdate()
    {
        float currSpeed = isDraining ? reducedMoveSpeed : moveSpeed;

        Vector2 input = Vector2.zero;
        if (joystick != null)
            input = joystick.Direction;

        UpdateCameraBounds();

        Vector2 nextPosition = rb.position + input * currSpeed * Time.fixedDeltaTime;
        float minX = minBounds.x + halfWidth;
        float maxX = maxBounds.x - halfWidth;
        float minY = minBounds.y + halfHeight;
        float maxY = maxBounds.y - halfHeight;

        nextPosition.x = minX <= maxX
            ? Mathf.Clamp(nextPosition.x, minX, maxX)
            : (minBounds.x + maxBounds.x) * 0.5f;
        nextPosition.y = minY <= maxY
            ? Mathf.Clamp(nextPosition.y, minY, maxY)
            : (minBounds.y + maxBounds.y) * 0.5f;

        rb.MovePosition(nextPosition);
    }

    public void SetDraining(bool draining)
    {
        isDraining = draining;
    }

    public void MultiplyMoveSpeed(float multiplier)
    {
        if (multiplier <= 0f)
            return;

        moveSpeed *= multiplier;
        reducedMoveSpeed *= multiplier;
    }

    public void ResetPowerUpModifiers()
    {
        moveSpeed = baseMoveSpeed;
        reducedMoveSpeed = baseReducedMoveSpeed;
    }

    public void ResetForNewRun()
    {
        isDraining = false;
        transform.SetPositionAndRotation(startingPosition, startingRotation);

        if (rb != null)
        {
            rb.position = (Vector2)startingPosition;
            rb.rotation = startingRotation.eulerAngles.z;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        UpdateCameraBounds();
    }

    private void CreateScaledPlayerVisual()
    {
        SpriteRenderer originalRenderer = GetComponent<SpriteRenderer>();
        if (originalRenderer == null)
            return;

        GameObject visualObject = new GameObject("PlayerVisual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localScale = Vector3.one * visualScaleMultiplier;

        VisualRenderer = visualObject.AddComponent<SpriteRenderer>();
        CopyRendererSettings(originalRenderer, VisualRenderer);

        // The enlarged sprite is visual only, so the player collider and drain
        // radius keep their existing gameplay sizes.
        originalRenderer.enabled = false;
    }

    private static void CopyRendererSettings(SpriteRenderer source, SpriteRenderer target)
    {
        target.sprite = source.sprite;
        target.color = source.color;
        target.sharedMaterial = source.sharedMaterial;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.drawMode = source.drawMode;
        target.size = source.size;
    }

    // If screen can rotate / resize, call this when resolution changes
    private void OnEnable()
    {
        UpdateCameraBounds();
    }
}
