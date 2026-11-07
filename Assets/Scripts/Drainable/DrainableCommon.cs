using UnityEngine;

public class DrainableCommon : DrainableBase
{
    // Movement speed is DrainableBase.moveSpeed (one Inspector field).
    public float directionChangeInterval = 2.5f;
    public float boundsPadding = 0.5f; // keep away from exact edges

    private Vector2 moveDir;
    private float dirTimer;

    private Camera cam;
    private Vector3 arenaMin;
    private Vector3 arenaMax;
    private bool hasArenaBounds;

    void Start()
    {
        cam = Camera.main;
        CacheArenaBounds();
        PickNewDirection();
        dirTimer = directionChangeInterval;
    }

    protected override void Update()
    {
        base.Update();
        if (cam == null) return;
        if (DrainableSpawner.AreDrainablesPaused) return;

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

        if (!hasArenaBounds)
            CacheArenaBounds();

        if (!hasArenaBounds)
            return;

        Vector3 min = arenaMin + new Vector3(boundsPadding, boundsPadding, 0f);
        Vector3 max = arenaMax - new Vector3(boundsPadding, boundsPadding, 0f);

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

    private void CacheArenaBounds()
    {
        if (DrainableSpawner.Instance != null && DrainableSpawner.Instance.TryGetArenaBounds(out arenaMin, out arenaMax))
        {
            hasArenaBounds = true;
            return;
        }

        if (cam == null)
            return;

        float planeDistance = Mathf.Abs(cam.transform.position.z - transform.position.z);
        arenaMin = cam.ViewportToWorldPoint(new Vector3(0f, 0f, planeDistance));
        arenaMax = cam.ViewportToWorldPoint(new Vector3(1f, 1f, planeDistance));
        hasArenaBounds = true;
    }
}
