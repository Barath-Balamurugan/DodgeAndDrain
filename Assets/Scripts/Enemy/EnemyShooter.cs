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
    public float shotTelegraphDuration = 0.35f;
    public float aimLineLength = 4.5f;
    public Color aimLineColor = new Color(1f, 0.3f, 0.35f, 0.9f);

    private Transform playerTransform;
    private float fireTimer;
    private bool isTelegraphingShot;
    private LineRenderer aimLine;

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            playerTransform = GameManager.Instance.player.transform;

        fireTimer = 1f / Mathf.Max(0.01f, fireRate);
        CreateAimLine();
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

        if (isTelegraphingShot)
        {
            UpdateAimLine(dirToPlayer);
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                Shoot(dirToPlayer);
                isTelegraphingShot = false;
                aimLine.enabled = false;
                fireTimer = Mathf.Max(0f, 1f / Mathf.Max(0.01f, fireRate) - shotTelegraphDuration);
            }
            return;
        }

        // Telegraph before each shot so players can recognise and dodge it.
        fireTimer -= Time.deltaTime;
        if (inRangeBand && fireTimer <= 0f)
        {
            isTelegraphingShot = true;
            fireTimer = shotTelegraphDuration;
            aimLine.enabled = true;
            UpdateAimLine(dirToPlayer);
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

    private void CreateAimLine()
    {
        GameObject lineObject = new GameObject("ShotWarningLine");
        lineObject.transform.SetParent(transform, false);

        aimLine = lineObject.AddComponent<LineRenderer>();
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        aimLine.material = spriteRenderer.sharedMaterial;
        aimLine.useWorldSpace = false;
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.025f;
        aimLine.endWidth = 0.05f;
        aimLine.startColor = aimLineColor;
        aimLine.endColor = aimLineColor;
        aimLine.sortingLayerID = spriteRenderer.sortingLayerID;
        aimLine.sortingOrder = spriteRenderer.sortingOrder + 1;
        aimLine.enabled = false;
    }

    private void UpdateAimLine(Vector2 direction)
    {
        if (aimLine == null)
            return;

        aimLine.SetPosition(0, Vector3.zero);
        aimLine.SetPosition(1, direction.normalized * aimLineLength);
    }
}
