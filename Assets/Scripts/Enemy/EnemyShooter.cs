using UnityEngine;

public class EnemyShooter : EnemyBase
{
    [Header("Movement")]
    public float desiredDistance = 5f;     // sweet spot distance from player
    public float distanceTolerance = 0.75f; // how much it can drift before correcting
    public float moveSpeedShooter = 2.5f;  // overrides moveSpeed feel (optional)

    [Header("Shooting")]
    public GameObject projectilePrefab;
    public Transform firePoint;            // optional; if null uses enemy position
    public float fireRate = 1.2f;          // shots per second
    public float projectileSpeed = 7f;
    public float projectileLifetime = 4f;

    private Transform playerTransform;
    private float fireTimer;

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            playerTransform = GameManager.Instance.player.transform;

        fireTimer = 1f / Mathf.Max(0.01f, fireRate);
    }

    protected override void Update()
    {
        base.Update();

        if (playerTransform == null || GameManager.Instance == null || GameManager.Instance.IsGameOver)
            return;

        Vector2 toPlayer = (Vector2)(playerTransform.position - transform.position);
        float dist = toPlayer.magnitude;

        Vector2 dirToPlayer = dist > 0.001f ? toPlayer.normalized : Vector2.zero;

        // Maintain distance band
        float minDist = desiredDistance - distanceTolerance;
        float maxDist = desiredDistance + distanceTolerance;

        bool inRangeBand = dist >= minDist && dist <= maxDist;

        if (!inRangeBand)
        {
            // Move closer if too far; move away if too close
            Vector2 moveDir = dist > desiredDistance ? dirToPlayer : -dirToPlayer;
            float speed = moveSpeedShooter > 0 ? moveSpeedShooter : moveSpeed;
            transform.position += (Vector3)(moveDir * speed * Time.deltaTime);
        }

        // Shoot only when inside band (or you can allow always if you prefer)
        fireTimer -= Time.deltaTime;
        if (inRangeBand && fireTimer <= 0f)
        {
            Shoot(dirToPlayer);
            fireTimer = 1f / Mathf.Max(0.01f, fireRate);
        }
    }

    private void Shoot(Vector2 dirToPlayer)
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = dirToPlayer * projectileSpeed;
        }

        Destroy(proj, projectileLifetime);
    }
}
