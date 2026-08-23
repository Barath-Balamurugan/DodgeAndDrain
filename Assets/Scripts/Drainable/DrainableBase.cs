using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DrainableBase : MonoBehaviour
{
    public float maxHealth = 20f;
    public float moveSpeed = 0.2f;
    public int scoreValue = 1;

    protected float currentHealth;

    private Vector3 originalScale;
    [SerializeField] private float minScaleFactor = 0.3f;

    private LineRenderer drainLine;
    private Transform drainTarget;
    public bool isBeingDrained;
    [SerializeField] private float drainBeamWidth = 0.16f;
    private float drainBeamBaseWidth;

    [Header("Energy Visuals")]
    [SerializeField, Min(0.1f)] private float visualScaleMultiplier = 1.75f;
    [SerializeField, Min(0f)] private float pulseAmount = 0.07f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 1.5f;
    [SerializeField, Range(0f, 1f)] private float whiteGlowMinAlpha = 0.12f;
    [SerializeField, Range(0f, 1f)] private float whiteGlowMaxAlpha = 0.32f;
    private Transform energyVisual;
    private Transform energyGlow;
    private Vector3 energyVisualBaseScale;
    private Vector3 energyGlowBaseScale;
    private SpriteRenderer energyGlowRenderer;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        CreateEnergyVisuals();

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
        UpdateEnergyVisuals();

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

    private void CreateEnergyVisuals()
    {
        SpriteRenderer originalRenderer = GetComponent<SpriteRenderer>();
        if (originalRenderer == null)
            return;

        energyVisual = CreateVisualChild("EnergyVisual", originalRenderer, visualScaleMultiplier, originalRenderer.color, originalRenderer.sortingOrder);
        energyGlow = CreateVisualChild("EnergyGlow", originalRenderer, visualScaleMultiplier * 1.22f, new Color(1f, 1f, 1f, whiteGlowMinAlpha), originalRenderer.sortingOrder - 1);
        energyGlowRenderer = energyGlow.GetComponent<SpriteRenderer>();
        energyVisualBaseScale = energyVisual.localScale;
        energyGlowBaseScale = energyGlow.localScale;

        // Keep the object's existing colour on the foreground visual. The extra
        // white sprite behind it provides the glow without recolouring the energy.
        originalRenderer.enabled = false;
    }

    private Transform CreateVisualChild(string objectName, SpriteRenderer source, float scale, Color color, int sortingOrder)
    {
        GameObject visualObject = new GameObject(objectName);
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.color = color;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.drawMode = source.drawMode;
        renderer.size = source.size;

        return visualObject.transform;
    }

    private void UpdateEnergyVisuals()
    {
        if (energyVisual == null || energyGlow == null)
            return;

        float pulse = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        energyVisual.localScale = energyVisualBaseScale * (1f + pulseAmount * pulse);
        energyGlow.localScale = energyGlowBaseScale * (1f + pulseAmount * 1.7f * pulse);

        if (energyGlowRenderer != null)
            energyGlowRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(whiteGlowMinAlpha, whiteGlowMaxAlpha, pulse));
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

    // Returns how much HP was actually drained
    public virtual float TakeDrain(float amount)
    {
        if (currentHealth <= 0f) return 0f;

        isBeingDrained = true;

        float before = currentHealth;
        currentHealth -= amount;

        UpdateVisuals();

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }

        return before - currentHealth;
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
