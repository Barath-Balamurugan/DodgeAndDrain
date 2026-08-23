using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [Header("Projectile")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 12f;
    public float fireCooldown = 0.12f;

    [Header("Ammo")]
    public PlayerAmmo ammo;
    public int bulletCost = 1;

    [Header("Damage")]
    public float baseDamage = 2f;

    [Header("Auto Target")]
    public float autoTargetMaxRange = 999f;

    private float bulletCooldownTimer;

    private void Awake()
    {
        if (ammo == null)
            ammo = GetComponent<PlayerAmmo>();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        bulletCooldownTimer -= Time.deltaTime;
    }

    public void ShootClosestEnemy()
    {
        if (bulletPrefab == null) return;
        if (bulletCooldownTimer > 0f) return;

        // Spend only when shooting
        if (ammo == null || !ammo.Spend(bulletCost))
            return;

        EnemyBase target = FindClosestEnemy(autoTargetMaxRange);

        Vector2 dir = target != null
            ? ((Vector2)target.transform.position - (Vector2)GetFirePos()).normalized
            : Vector2.up;

        FireProjectile(dir);
        bulletCooldownTimer = fireCooldown;
    }

    private void FireProjectile(Vector2 dir)
    {
        Vector3 spawnPos = GetFirePos();
        GameObject b = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        PlayerBullet pb = b.GetComponent<PlayerBullet>();
        if (pb != null) pb.damage = baseDamage;

        Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = dir.normalized * bulletSpeed; // ✅ compatibility
        }
    }

    private Vector3 GetFirePos()
    {
        return firePoint != null ? firePoint.position : transform.position;
    }

    private EnemyBase FindClosestEnemy(float maxRange)
    {
        EnemyBase[] all = FindObjectsOfType<EnemyBase>();

        EnemyBase best = null;
        float bestDistSq = maxRange * maxRange;
        Vector2 myPos = transform.position;

        for (int i = 0; i < all.Length; i++)
        {
            EnemyBase e = all[i];
            if (e == null) continue;

            float d = ((Vector2)e.transform.position - myPos).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = e;
            }
        }
        return best;
    }
}
