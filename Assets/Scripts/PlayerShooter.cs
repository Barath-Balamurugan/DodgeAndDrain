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

    [Header("Muzzle Flash")]
    [SerializeField] private Color muzzleFlashColor = new Color(0.7f, 1f, 1f, 0.95f);
    [SerializeField, Min(0.01f)] private float muzzleFlashDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float muzzleFlashLength = 0.6f;
    [SerializeField, Min(0.01f)] private float muzzleFlashWidth = 0.24f;

    private float bulletCooldownTimer;
    private static Sprite muzzleFlashSprite;

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

        PlayMuzzleFlash(spawnPos, dir);
    }

    private void PlayMuzzleFlash(Vector3 position, Vector2 direction)
    {
        GameObject flashObject = new GameObject("MuzzleFlash");
        flashObject.transform.position = position;
        flashObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        SpriteRenderer flashRenderer = flashObject.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = GetMuzzleFlashSprite();
        flashRenderer.color = muzzleFlashColor;

        SpriteRenderer playerRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (playerRenderer != null)
        {
            flashRenderer.sharedMaterial = playerRenderer.sharedMaterial;
            flashRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            flashRenderer.sortingOrder = playerRenderer.sortingOrder + 3;
        }
        else
        {
            flashRenderer.sortingOrder = 10;
        }

        StartCoroutine(PlayMuzzleFlashRoutine(flashObject.transform, flashRenderer));
    }

    private System.Collections.IEnumerator PlayMuzzleFlashRoutine(Transform flashTransform, SpriteRenderer flashRenderer)
    {
        Vector3 startScale = new Vector3(muzzleFlashLength, muzzleFlashWidth, 1f);
        Vector3 endScale = startScale * 1.45f;
        float elapsed = 0f;

        while (elapsed < muzzleFlashDuration)
        {
            float progress = elapsed / muzzleFlashDuration;
            flashTransform.localScale = Vector3.Lerp(startScale, endScale, progress);
            flashRenderer.color = new Color(
                muzzleFlashColor.r,
                muzzleFlashColor.g,
                muzzleFlashColor.b,
                Mathf.Lerp(muzzleFlashColor.a, 0f, progress));

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(flashTransform.gameObject);
    }

    private static Sprite GetMuzzleFlashSprite()
    {
        if (muzzleFlashSprite != null)
            return muzzleFlashSprite;

        const int textureSize = 32;
        Color32[] pixels = new Color32[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float horizontal = (x + 0.5f) / textureSize * 2f - 1f;
                float vertical = (y + 0.5f) / textureSize * 2f - 1f;
                float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
                float alpha = Mathf.Clamp01(1f - distance);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "MuzzleFlashSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply();

        muzzleFlashSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        muzzleFlashSprite.name = "MuzzleFlashSprite";
        muzzleFlashSprite.hideFlags = HideFlags.HideAndDontSave;
        return muzzleFlashSprite;
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
