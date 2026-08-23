using UnityEngine;
using System.Collections;

public class EnemyDasher : EnemyBase
{
    [Header("Movement")]
    public float approachSpeed = 1.5f;

    [Header("Dash")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 1.5f;
    public float windupTime = 0.15f;
    public float dashRange = 4f;   // ⭐ only dash within this distance

    [Header("Visual Feedback")]
    public Color windupFlashColor = Color.red;
    public float flashSpeed = 15f;

    private Transform playerTransform;

    private SpriteRenderer sr;  
    private Color originalColor;
    private bool isFlashing;

    private bool isDashing;
    private bool dashOnCooldown;
    private Vector2 dashDir;

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            playerTransform = GameManager.Instance.player.transform;

        EnemyShapeVisual shapeVisual = GetComponent<EnemyShapeVisual>();
        sr = shapeVisual != null ? shapeVisual.Renderer : GetComponent<SpriteRenderer>();
        if (sr != null)
            originalColor = sr.color;
    }

    protected override void Update()
    {
        base.Update();

        if (playerTransform == null || GameManager.Instance == null || GameManager.Instance.IsGameOver)
            return;

        Vector2 toPlayer = (Vector2)(playerTransform.position - transform.position);
        float dist = toPlayer.magnitude;

        if (isDashing)
        {
            transform.position += (Vector3)(dashDir * dashSpeed * Time.deltaTime);
            return;
        }

        // Normal approach
        Vector2 dir = dist > 0.01f ? toPlayer.normalized : Vector2.zero;
        transform.position += (Vector3)(dir * approachSpeed * Time.deltaTime);

        // Try to dash only if:
        // - close enough
        // - not already dashing
        // - not on cooldown
        if (dist <= dashRange && !dashOnCooldown)
        {
            StartCoroutine(DashRoutine(dir));
        }
    }

    IEnumerator DashRoutine(Vector2 lockedDir)
    {
        dashOnCooldown = true;

        isFlashing = true;

        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;

            if (sr != null)
            {
                float lerp = Mathf.PingPong(Time.time * flashSpeed, 1f);
                sr.color = Color.Lerp(originalColor, windupFlashColor, lerp);
            }

            yield return null;
        }

        // Lock direction AFTER windup
        dashDir = lockedDir;
        isFlashing = false;

        if (sr != null)
            sr.color = originalColor;

        isDashing = true;
        yield return new WaitForSeconds(dashDuration);

        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        dashOnCooldown = false;
    }

}
