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

    private CircleCollider2D drainCollider;
    private ParticleSystem.EmissionModule emission;

    private void Awake()
    {
        drainCollider = GetComponent<CircleCollider2D>();
        if (ringSprite == null)
            ringSprite = GetComponent<SpriteRenderer>();
        if (drainParticles != null)
            emission = drainParticles.emission;

        // scale ring to match collider
        // if (ringSprite != null && drainCollider != null)
        // {
        //     float r = drainCollider.radius;
        //     transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
        // }
    }

    private void Update()
    {
        if (player == null || gameManager == null) return;

        bool active = player.isDraining && !gameManager.IsOverheated;

        if (ringSprite != null)
        {
            ringSprite.enabled = active;
        }

        if (drainParticles != null)
        {
            emission.enabled = active;
        }

        if (!active || ringSprite == null) return;

        // pulse ring alpha while draining
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Color c = ringSprite.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        ringSprite.color = c;
    }
}
