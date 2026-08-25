using UnityEngine;

public class PlayerBullet : MonoBehaviour
{
    public float damage = 2f;
    public float lifeTime = 3f;

    [Header("Projectile Trail")]
    [SerializeField, Min(0.01f)] private float trailDuration = 0.16f;
    [SerializeField, Min(0f)] private float trailStartWidth = 0.075f;
    [SerializeField, Min(0f)] private float trailEndWidth = 0.012f;
    [SerializeField] private Color trailColor = new Color(0.7f, 1f, 1f, 0.9f);

    [Header("Hit Feedback")]
    [SerializeField, Range(1, 16)] private int hitSparkCount = 6;
    [SerializeField, Min(0f)] private float hitSparkSpeed = 2.6f;
    [SerializeField, Min(0.01f)] private float hitSparkLifetime = 0.18f;

    private void Awake()
    {
        ConfigureTrail();
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void ConfigureTrail()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        trail.time = trailDuration;
        trail.minVertexDistance = 0.025f;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        trail.material = spriteRenderer != null ? spriteRenderer.sharedMaterial : trail.material;
        trail.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        trail.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.PlayHitFeedback();
            CreateHitSparks(transform.position);
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }

    private void CreateHitSparks(Vector3 position)
    {
        GameObject sparkObject = new GameObject("HitSparks");
        sparkObject.transform.position = position;

        ParticleSystem particles = sparkObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = hitSparkLifetime;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = hitSparkLifetime;
        main.startSpeed = hitSparkSpeed;
        main.startSize = 0.09f;
        main.startColor = Color.white;
        main.maxParticles = hitSparkCount;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)hitSparkCount) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.02f;
        shape.radiusThickness = 1f;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null && spriteRenderer != null)
        {
            particleRenderer.material = spriteRenderer.sharedMaterial;
            particleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            particleRenderer.sortingOrder = spriteRenderer.sortingOrder + 2;
        }

        particles.Play();
        Destroy(sparkObject, hitSparkLifetime + 0.1f);
    }
}
