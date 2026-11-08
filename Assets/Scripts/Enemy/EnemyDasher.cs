using UnityEngine;
using System.Collections;

public class EnemyDasher : EnemyBase
{
    private const int TelegraphSegments = 32;

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
    public float windupTelegraphRadius = 0.42f;
    public float windupLineLength = 1.4f;

    private Transform playerTransform;

    private SpriteRenderer sr;  
    private Color originalColor;
    private LineRenderer windupRing;
    private LineRenderer windupLine;

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
        {
            originalColor = sr.color;
            CreateWindupTelegraph();
        }
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

        SetWindupTelegraphVisible(true);

        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            UpdateWindupTelegraph(lockedDir, Mathf.Clamp01(t / windupTime));

            if (sr != null)
            {
                float lerp = Mathf.PingPong(Time.time * flashSpeed, 1f);
                sr.color = Color.Lerp(originalColor, windupFlashColor, lerp);
            }

            yield return null;
        }

        // Lock direction AFTER windup
        dashDir = lockedDir;

        if (sr != null)
            sr.color = originalColor;

        SetWindupTelegraphVisible(false);
        isDashing = true;
        yield return new WaitForSeconds(dashDuration);

        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        dashOnCooldown = false;
    }

    private void CreateWindupTelegraph()
    {
        windupRing = CreateTelegraphLine("DashWarningRing", true);
        windupRing.positionCount = TelegraphSegments + 1;

        windupLine = CreateTelegraphLine("DashWarningLine", false);
        windupLine.positionCount = 2;
    }

    private LineRenderer CreateTelegraphLine(string objectName, bool isLoop)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = isLoop;
        line.material = sr.sharedMaterial;
        line.startWidth = 0.035f;
        line.endWidth = 0.035f;
        line.sortingLayerID = sr.sortingLayerID;
        line.sortingOrder = sr.sortingOrder + 1;
        line.enabled = false;
        return line;
    }

    private void UpdateWindupTelegraph(Vector2 direction, float progress)
    {
        if (windupRing == null || windupLine == null)
            return;

        float radius = Mathf.Lerp(0.08f, windupTelegraphRadius, progress);
        for (int i = 0; i <= TelegraphSegments; i++)
        {
            float angle = i / (float)TelegraphSegments * Mathf.PI * 2f;
            windupRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        Color warningColor = windupFlashColor;
        warningColor.a = Mathf.Lerp(0.35f, 1f, progress);
        windupRing.startColor = warningColor;
        windupRing.endColor = warningColor;
        windupLine.startColor = warningColor;
        windupLine.endColor = warningColor;
        windupLine.SetPosition(0, Vector3.zero);
        windupLine.SetPosition(1, direction.normalized * windupLineLength);
    }

    private void SetWindupTelegraphVisible(bool visible)
    {
        if (windupRing != null)
            windupRing.enabled = visible;
        if (windupLine != null)
            windupLine.enabled = visible;
    }

}
