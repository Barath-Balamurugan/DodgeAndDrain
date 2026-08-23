using UnityEngine;

public class DrainableCommon : DrainableBase
{
    public float moveSpeed = 2f;
    public float directionChangeInterval = 2.5f;
    public float boundsPadding = 0.5f; // keep away from exact edges

    private Vector2 moveDir;
    private float dirTimer;

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        PickNewDirection();
        dirTimer = directionChangeInterval;
    }

    void Update()
    {
        base.Update();
        if (cam == null) return;

        // Move
        transform.position += (Vector3)(moveDir * moveSpeed * Time.deltaTime);

        // Change direction occasionally
        dirTimer -= Time.deltaTime;
        if (dirTimer <= 0f)
        {
            PickNewDirection();
            dirTimer = directionChangeInterval;
        }

        ClampAndRedirect();
    }

    void PickNewDirection()
    {
        moveDir = Random.insideUnitCircle.normalized;
        if (moveDir == Vector2.zero)
            moveDir = Vector2.right;
    }

    void ClampAndRedirect()
    {
        Vector3 pos = transform.position;

        Vector3 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, cam.nearClipPlane));
        Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, cam.nearClipPlane));

        min += Vector3.one * boundsPadding;
        max -= Vector3.one * boundsPadding;

        bool bounced = false;

        if (pos.x < min.x || pos.x > max.x)
        {
            moveDir.x *= -1f;
            bounced = true;
        }

        if (pos.y < min.y || pos.y > max.y)
        {
            moveDir.y *= -1f;
            bounced = true;
        }

        if (bounced)
            PickNewDirection(); // optional: adds organic movement

        pos.x = Mathf.Clamp(pos.x, min.x, max.x);
        pos.y = Mathf.Clamp(pos.y, min.y, max.y);
        pos.z = 0f;

        transform.position = pos;
    }
}
