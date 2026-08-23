using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    private void Awake()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        trail.time = 0.22f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = 0.06f;
        trail.endWidth = 0.008f;
        trail.startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
        trail.material = spriteRenderer != null ? spriteRenderer.sharedMaterial : null;
        trail.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        trail.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
            return;

        if (other.GetComponent<PlayerMovement2D>() != null)
        {
            GameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }
}
