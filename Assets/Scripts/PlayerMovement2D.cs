using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public Joystick joystick;
    public float moveSpeed = 5f;
    public float reducedMoveSpeed = 2f;

    public bool isDraining;

    private Rigidbody2D rb;

    private Vector2 minBounds;
    private Vector2 maxBounds;
    private float halfWidth;
    private float halfHeight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Start()
    {
        UpdateCameraBounds();
        CachePlayerExtents();
    }

    private void UpdateCameraBounds()
    {
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
        // Use SpriteRenderer if present
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
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

        // Move with physics velocity
        rb.linearVelocity = input * currSpeed;

        // Clamp position so player stays in view
        Vector2 pos = rb.position;

        float clampedX = Mathf.Clamp(pos.x, minBounds.x + halfWidth,  maxBounds.x - halfWidth);
        float clampedY = Mathf.Clamp(pos.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);

        rb.position = new Vector2(clampedX, clampedY);
    }

    public void SetDraining(bool draining)
    {
        isDraining = draining;
    }

    // If screen can rotate / resize, call this when resolution changes
    private void OnEnable()
    {
        UpdateCameraBounds();
    }
}
