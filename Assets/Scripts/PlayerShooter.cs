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

    [Header("Hold To Fire")]
    [Tooltip("While the Shoot button is held, keep firing every fireCooldown seconds.")]
    [SerializeField] private bool holdToFire = true;

    [Header("Recoil Kick")]
    [SerializeField, Min(0f)] private float recoilDistance = 0.16f;
    [SerializeField, Min(0.01f)] private float recoilDuration = 0.13f;
    [SerializeField, Range(0f, 0.5f)] private float recoilSquashAmount = 0.16f;

    [Header("Shoot Sound")]
    [Tooltip("Optional. A short generated zap is used when no clip is assigned.")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField, Range(0f, 1f)] private float shootVolume = 0.35f;
    [SerializeField, Range(0f, 0.25f)] private float shootPitchVariation = 0.06f;

    [Header("Empty Ammo Sound")]
    [Tooltip("Optional. A generated two-pulse empty-ammo beep is used when no clip is assigned.")]
    [SerializeField] private AudioClip emptyAmmoSound;
    [SerializeField, Range(0f, 1f)] private float emptyAmmoVolume = 0.42f;

    private float bulletCooldownTimer;
    private bool triggerHeld;
    private bool emptyFeedbackPlayedThisHold;
    private static AudioClip generatedShootSound;
    private static AudioClip generatedEmptyAmmoSound;
    private AudioSource shootAudioSource;
    private Coroutine recoilRoutine;
    private float initialFireCooldown;
    private float initialDamage;
    private Transform recoilVisual;
    private Vector3 recoilVisualRestPosition;
    private Vector3 recoilVisualRestScale;
    private bool hasRecoilVisualRestTransform;

    private void Awake()
    {
        initialFireCooldown = fireCooldown;
        initialDamage = baseDamage;

        if (ammo == null)
            ammo = GetComponent<PlayerAmmo>();

        shootAudioSource = GetComponent<AudioSource>();
        if (shootAudioSource == null)
            shootAudioSource = gameObject.AddComponent<AudioSource>();

        shootAudioSource.playOnAwake = false;
        shootAudioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        CacheRecoilVisualRestTransform();
    }

    public void MultiplyFireRate(float multiplier)
    {
        if (multiplier <= 0f)
            return;

        fireCooldown = Mathf.Max(0.02f, fireCooldown / multiplier);
    }

    public void MultiplyDamage(float multiplier)
    {
        if (multiplier > 0f)
            baseDamage *= multiplier;
    }

    public void ResetPowerUpModifiers()
    {
        fireCooldown = initialFireCooldown;
        baseDamage = initialDamage;
    }

    public void ResetForNewRun()
    {
        bulletCooldownTimer = 0f;
        triggerHeld = false;

        if (recoilRoutine != null)
            StopCoroutine(recoilRoutine);

        recoilRoutine = null;
        CacheRecoilVisualRestTransform();

        if (recoilVisual != null)
        {
            recoilVisual.localPosition = recoilVisualRestPosition;
            recoilVisual.localScale = recoilVisualRestScale;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            triggerHeld = false;
            return;
        }

        bulletCooldownTimer -= Time.deltaTime;

        if (triggerHeld && bulletCooldownTimer <= 0f)
            TryShoot(true);
    }

    private void OnDisable()
    {
        triggerHeld = false;
    }

    // Shoot button PointerDown: fire right away, then keep firing while held.
    public void PressTrigger()
    {
        triggerHeld = holdToFire;
        emptyFeedbackPlayedThisHold = false;
        TryShoot(false);
    }

    // Shoot button PointerUp.
    public void ReleaseTrigger()
    {
        triggerHeld = false;
    }

    // Single shot. Kept so anything still wired to it keeps working.
    public void ShootClosestEnemy()
    {
        TryShoot(false);
    }

    private void TryShoot(bool isHeldRepeat)
    {
        if (bulletPrefab == null) return;
        if (bulletCooldownTimer > 0f) return;

        EnemyBase target = FindClosestEnemy(autoTargetMaxRange, true);

        // Held fire waits for an enemy that still needs damage, so holding the
        // button between waves or at a doomed enemy doesn't burn ammo. A
        // deliberate press still fires at whatever is closest.
        if (target == null)
        {
            if (isHeldRepeat) return;
            target = FindClosestEnemy(autoTargetMaxRange, false);
        }

        // Spend only when shooting
        if (ammo == null || !ammo.Spend(bulletCost))
        {
            if (!isHeldRepeat || !emptyFeedbackPlayedThisHold)
                PlayEmptyAmmoFeedback();

            emptyFeedbackPlayedThisHold = true;
            return;
        }

        Vector2 dir = target != null
            ? ((Vector2)target.transform.position - (Vector2)GetFirePos()).normalized
            : Vector2.up;

        FireProjectile(dir, target);
        bulletCooldownTimer = fireCooldown;
    }

    private void FireProjectile(Vector2 dir, EnemyBase target)
    {
        Vector3 spawnPos = GetFirePos();
        GameObject b = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        PlayerBullet pb = b.GetComponent<PlayerBullet>();
        if (pb != null)
        {
            pb.damage = baseDamage;
            pb.ReserveDamageOn(target);
        }

        Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = dir.normalized * bulletSpeed; // ✅ compatibility
        }

        PlayRecoilKick(dir);
        PlayShootSound();
    }

    private void PlayRecoilKick(Vector2 shotDirection)
    {
        PlayerMovement2D playerMovement = GetComponent<PlayerMovement2D>();
        Transform visualTransform = playerMovement != null && playerMovement.VisualRenderer != null
            ? playerMovement.VisualRenderer.transform
            : null;

        if (visualTransform == null)
            return;

        CacheRecoilVisualRestTransform();

        if (recoilRoutine != null)
            StopCoroutine(recoilRoutine);

        Vector3 localKickDirection = visualTransform.parent.InverseTransformDirection(-shotDirection.normalized);
        recoilRoutine = StartCoroutine(PlayRecoilKickRoutine(visualTransform, localKickDirection));
    }

    private void CacheRecoilVisualRestTransform()
    {
        PlayerMovement2D playerMovement = GetComponent<PlayerMovement2D>();
        Transform visualTransform = playerMovement != null && playerMovement.VisualRenderer != null
            ? playerMovement.VisualRenderer.transform
            : null;

        if (visualTransform == null)
            return;

        if (!hasRecoilVisualRestTransform || visualTransform != recoilVisual)
        {
            recoilVisual = visualTransform;
            recoilVisualRestPosition = visualTransform.localPosition;
            recoilVisualRestScale = visualTransform.localScale;
            hasRecoilVisualRestTransform = true;
        }
    }

    private System.Collections.IEnumerator PlayRecoilKickRoutine(Transform visualTransform, Vector3 localKickDirection)
    {
        // Always kick from the cached rest pose. Reading the current pose here would
        // capture a half-finished kick whenever shots come faster than recoilDuration
        // (hold-to-fire), and the squash would compound into a permanently stretched,
        // off-centre player.
        Vector3 restPosition = recoilVisualRestPosition;
        Vector3 restScale = recoilVisualRestScale;
        float elapsed = 0f;

        while (elapsed < recoilDuration)
        {
            float progress = elapsed / recoilDuration;
            float kickAmount = Mathf.Sin(progress * Mathf.PI) * recoilDistance;
            visualTransform.localPosition = restPosition + localKickDirection * kickAmount;

            float squash = Mathf.Sin(progress * Mathf.PI) * recoilSquashAmount;
            visualTransform.localScale = new Vector3(
                restScale.x * (1f - squash),
                restScale.y * (1f + squash),
                restScale.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        visualTransform.localPosition = restPosition;
        visualTransform.localScale = restScale;
        recoilRoutine = null;
    }

    private void PlayShootSound()
    {
        if (shootAudioSource == null)
            return;

        AudioClip clip = shootSound != null ? shootSound : GetGeneratedShootSound();
        shootAudioSource.pitch = Random.Range(1f - shootPitchVariation, 1f + shootPitchVariation);
        shootAudioSource.PlayOneShot(clip, shootVolume);
    }

    private void PlayEmptyAmmoFeedback()
    {
        if (ammo != null)
            ammo.PlayEmptyAmmoFeedback();

        if (shootAudioSource == null)
            return;

        AudioClip clip = emptyAmmoSound != null ? emptyAmmoSound : GetGeneratedEmptyAmmoSound();
        shootAudioSource.pitch = 1f;
        shootAudioSource.PlayOneShot(clip, emptyAmmoVolume);
    }

    private static AudioClip GetGeneratedShootSound()
    {
        if (generatedShootSound != null)
            return generatedShootSound;

        const int sampleRate = 44100;
        const float duration = 0.075f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            float frequency = Mathf.Lerp(1150f, 420f, progress);
            float envelope = Mathf.Pow(1f - progress, 2.2f);
            phase += 2f * Mathf.PI * frequency / sampleRate;
            samples[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * envelope * 0.5f;
        }

        generatedShootSound = AudioClip.Create("GeneratedShootZap", sampleCount, 1, sampleRate, false);
        generatedShootSound.SetData(samples, 0);
        generatedShootSound.hideFlags = HideFlags.HideAndDontSave;
        return generatedShootSound;
    }

    private static AudioClip GetGeneratedEmptyAmmoSound()
    {
        if (generatedEmptyAmmoSound != null)
            return generatedEmptyAmmoSound;

        const int sampleRate = 44100;
        const float duration = 0.14f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float pulseStart = time < 0.045f ? 0f : 0.07f;
            float pulseProgress = (time - pulseStart) / 0.045f;
            bool inPulse = (time < 0.045f) || (time >= 0.07f && time < 0.115f);

            if (!inPulse)
                continue;

            float envelope = Mathf.Sin(Mathf.Clamp01(pulseProgress) * Mathf.PI);
            samples[i] = Mathf.Sin(time * 2f * Mathf.PI * 340f) * envelope * 0.35f;
        }

        generatedEmptyAmmoSound = AudioClip.Create("GeneratedEmptyAmmoClick", sampleCount, 1, sampleRate, false);
        generatedEmptyAmmoSound.SetData(samples, 0);
        generatedEmptyAmmoSound.hideFlags = HideFlags.HideAndDontSave;
        return generatedEmptyAmmoSound;
    }

    private Vector3 GetFirePos()
    {
        return firePoint != null ? firePoint.position : transform.position;
    }

    private EnemyBase FindClosestEnemy(float maxRange, bool skipCoveredEnemies)
    {
        EnemyBase[] all = FindObjectsOfType<EnemyBase>();

        EnemyBase best = null;
        float bestDistSq = maxRange * maxRange;
        Vector2 myPos = transform.position;

        for (int i = 0; i < all.Length; i++)
        {
            EnemyBase e = all[i];
            if (e == null) continue;
            if (skipCoveredEnemies && e.IsCoveredByIncomingShots) continue;

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
