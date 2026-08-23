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

    protected virtual void Awake()
    {
        currentHealth = maxHealth;

        drainLine = GetComponent<LineRenderer>();
        if (drainLine != null)
        {
            drainLine.enabled = false; // hide at start
            drainLine.positionCount = 2;
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
        }
        else if (drainLine != null)
        {
            drainLine.enabled = false;
        }
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
