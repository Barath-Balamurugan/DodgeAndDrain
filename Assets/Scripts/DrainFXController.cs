using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class DrainFXController : MonoBehaviour
{
    public PlayerMovement2D player;
    public GameManager gameManager;
    public SpriteRenderer ringSprite;
    public ParticleSystem drainParticles;

    public float pulseSpeed = 6f;
    public float minAlpha = 0.1f;
    public float maxAlpha = 0.4f;
    [SerializeField] private Color drainColor = new Color(0.15f, 0.95f, 1f);
    [SerializeField] private float ringPulseAmount = 0.07f;
    [SerializeField] private float particleTravelSpeed = 7f;
    [SerializeField] private float particleEmissionRate = 70f;

    private CircleCollider2D drainCollider;
    private ParticleSystem.EmissionModule emission;
    private Vector3 ringBaseScale;

    private void Awake()
    {
        drainCollider = GetComponent<CircleCollider2D>();
        if (ringSprite == null)
            ringSprite = GetComponent<SpriteRenderer>();

        ConfigureRing();

        if (drainParticles != null)
        {
            emission = drainParticles.emission;
            ConfigureInwardParticles();
        }
    }

    private void Update()
    {
        if (player == null || gameManager == null) return;

        bool active = player.isDraining && !gameManager.IsOverheated && !gameManager.IsGameOver;

        if (ringSprite != null)
        {
            ringSprite.enabled = active;
        }

        if (drainParticles != null)
        {
            emission.enabled = active;

            if (active && !drainParticles.isPlaying)
                drainParticles.Play();
        }

        if (!active || ringSprite == null) return;

        // pulse ring alpha while draining
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        ringSprite.color = new Color(drainColor.r, drainColor.g, drainColor.b, Mathf.Lerp(minAlpha, maxAlpha, t));
        ringSprite.transform.localScale = ringBaseScale * (1f + ringPulseAmount * t);
    }

    private void ConfigureRing()
    {
        if (ringSprite == null)
            return;

        ringSprite.sortingOrder = Mathf.Max(ringSprite.sortingOrder, 4);

        if (drainCollider != null && ringSprite.sprite != null)
        {
            Vector2 spriteSize = ringSprite.sprite.bounds.size;
            float diameter = drainCollider.radius * 2f;

            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                ringSprite.transform.localScale = new Vector3(
                    diameter / spriteSize.x,
                    diameter / spriteSize.y,
                    1f);
            }
        }

        ringBaseScale = ringSprite.transform.localScale;
        ringSprite.color = new Color(drainColor.r, drainColor.g, drainColor.b, minAlpha);
    }

    private void ConfigureInwardParticles()
    {
        ParticleSystem.MainModule main = drainParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.8f;
        main.startSpeed = -particleTravelSpeed;
        main.startSize = 0.14f;
        main.startColor = new Color(0.55f, 1f, 1f, 1f);

        ParticleSystem.ShapeModule shape = drainParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = drainCollider != null ? drainCollider.radius : shape.radius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        shape.alignToDirection = true;

        emission.rateOverTime = particleEmissionRate;
    }
}
