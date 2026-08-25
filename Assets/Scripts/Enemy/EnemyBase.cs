using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyBase : MonoBehaviour
{
    public float maxHealth = 20f;
    public float moveSpeed = 3f;
    public int scoreValue = 10;

    protected float currentHealth;

    private Vector3 originalScale;
    [SerializeField] private float minScaleFactor = 0.3f;

    private LineRenderer drainLine;
    private Transform drainTarget;
    public bool isBeingDrained;
    [SerializeField] private float drainBeamWidth = 0.16f;
    private float drainBeamBaseWidth;

    [Header("Visual Scale")]
    [SerializeField, Min(0.1f)] private float visualScaleMultiplier = 1.75f;
    private bool visualScaleApplied;

    [Header("Hit Feedback")]
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.07f;
    private Coroutine hitFlashRoutine;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;

        drainLine = GetComponent<LineRenderer>();
        if (drainLine != null)
        {
            drainLine.enabled = false; // hide at start
            drainLine.positionCount = 2;
            ConfigureDrainBeam();
        }

        originalScale = transform.localScale;
        UpdateVisuals();
    }

    protected virtual void Update()
    {
        // Update drain line position if active
        if (isBeingDrained && drainLine != null && drainTarget != null)
        {
            drainLine.enabled = true;
            drainLine.SetPosition(0, transform.position);       // enemy
            drainLine.SetPosition(1, drainTarget.position);     // player
            drainLine.widthMultiplier = drainBeamBaseWidth * (1f + 0.16f * Mathf.Sin(Time.time * 14f));
        }
        else if (drainLine != null)
        {
            drainLine.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!visualScaleApplied)
            ApplyVisualScale();
    }

    private void ApplyVisualScale()
    {
        EnemyShapeVisual shapeVisual = GetComponent<EnemyShapeVisual>();
        SpriteRenderer visualRenderer = shapeVisual != null ? shapeVisual.Renderer : GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
            return;

        if (visualRenderer.transform == transform)
        {
            GameObject visualObject = new GameObject("EnemyVisual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localScale = Vector3.one * visualScaleMultiplier;

            SpriteRenderer enlargedRenderer = visualObject.AddComponent<SpriteRenderer>();
            CopyRendererSettings(visualRenderer, enlargedRenderer);
            visualRenderer.enabled = false;
        }
        else
        {
            visualRenderer.transform.localScale *= visualScaleMultiplier;
        }

        // Scaling the sprite child keeps movement, collision, and spawn logic unchanged.
        visualScaleApplied = true;
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

    private void ConfigureDrainBeam()
    {
        drainBeamBaseWidth = drainBeamWidth;
        drainLine.useWorldSpace = true;
        drainLine.widthMultiplier = drainBeamBaseWidth;
        drainLine.numCapVertices = 6;
        drainLine.sortingOrder = Mathf.Max(drainLine.sortingOrder, 8);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.15f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.85f, 1f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        drainLine.colorGradient = gradient;
    }

    // Called from DrainZone whenever this enemy is being drained
    public void SetDrainingVisual(bool draining, Transform target)
    {
        isBeingDrained = draining;
        drainTarget = target;
    }

    public virtual void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth -= amount;
        UpdateVisuals();

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    public void PlayHitFeedback()
    {
        SpriteRenderer visualRenderer = GetVisibleSpriteRenderer();
        if (visualRenderer == null)
            return;

        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        hitFlashRoutine = StartCoroutine(PlayHitFlashRoutine(visualRenderer));
    }

    private System.Collections.IEnumerator PlayHitFlashRoutine(SpriteRenderer visualRenderer)
    {
        Color originalColor = visualRenderer.color;
        Color flashColor = Color.white;
        flashColor.a = originalColor.a;
        float elapsed = 0f;

        while (elapsed < hitFlashDuration)
        {
            float progress = elapsed / hitFlashDuration;
            visualRenderer.color = Color.Lerp(flashColor, originalColor, progress);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (visualRenderer != null)
            visualRenderer.color = originalColor;

        hitFlashRoutine = null;
    }

    private SpriteRenderer GetVisibleSpriteRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.enabled)
                return renderer;
        }

        return null;
    }

    protected void UpdateVisuals()
    {
        float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0f;

        // 1 = full size, minScaleFactor (e.g. 0.3) at 0 health
        float scaleFactor = Mathf.Lerp(minScaleFactor, 1f, healthPercent);

        transform.localScale = originalScale * scaleFactor;
    }

    protected virtual void Die()
    {
        if (drainLine != null)
        {
            drainLine.enabled = false;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreValue);
        }

        Destroy(gameObject);
    }

    public void StopDraining()
    {
        isBeingDrained = false;
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
            return;

        PlayerMovement2D player = collision.collider.GetComponent<PlayerMovement2D>();
        if (player != null)
        {
            GameManager.Instance.GameOver();
        }
    }
}
