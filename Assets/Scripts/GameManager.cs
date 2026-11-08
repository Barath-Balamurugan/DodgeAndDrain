using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Drain Overheat")]
    public float drainGaugeMax = 100f;
    public float overheatDuration = 3f;
    public float gaugeIncreasePerSecond = 20f;
    public float gaugeDecreasePerSecond = 30f;

    [Header("References")]
    public PlayerMovement2D player;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text energyText;
    public Image drainGaugeFill;
    public GameObject gameOverPanel;

    [Header("Drain UI")]
    [SerializeField] private TMP_Text drainHeatText;
    [SerializeField] private Color drainCoolColor = new Color(0.2f, 1f, 0.55f);
    [SerializeField] private Color drainWarmColor = new Color(1f, 0.72f, 0.2f);
    [SerializeField] private Color drainCriticalColor = new Color(1f, 0.25f, 0.35f);

    [Header("Death Feedback")]
    [SerializeField] private float deathFlashDuration = 0.16f;
    [SerializeField] private float deathFlashOpacity = 0.65f;
    [SerializeField] private float deathShakeDuration = 0.25f;
    [SerializeField] private float deathShakeStrength = 0.18f;
    [SerializeField] private bool vibrateOnDeath = true;

    [Header("Wave Reward Feedback")]
    [SerializeField] private Color drainHeatRewardColor = new Color(0.8f, 1f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float drainHeatSweepDuration = 0.35f;
    [SerializeField, Min(1f)] private float drainHeatSweepWidth = 120f;

    [Header("Overheat Nova (rare power-up)")]
    [SerializeField, Min(0.5f)] private float overheatNovaRadius = 6f;
    [Tooltip("High enough to destroy every enemy type (shooters have 45 HP).")]
    [SerializeField, Min(0f)] private float overheatNovaDamage = 100f;
    [SerializeField] private Color overheatNovaColor = new Color(1f, 0.35f, 0.5f, 1f);

    [Header("Runtime (debug)")]
    public float drainGauge;
    public bool overheated;
    public float currentEnergy;
    public float currentScore;
    public float bestScore;

    private float overheatTimer;
    private bool isGameOver;
    private Image deathFlash;
    private Image drainHeatRewardSweep;
    private Coroutine deathFeedbackRoutine;
    private Coroutine drainHeatRewardRoutine;
    private Transform shakenCamera;
    private Vector3 cameraBaseLocalPosition;
    private float baseGaugeIncreasePerSecond;
    private bool overheatNovaEnabled;
    private GameOverScreen gameOverScreen;
    private EnemySpawner enemySpawner;
    private bool reviveUsed;
    private bool newBestThisRun;

    public bool IsOverheated => overheated;
    public bool IsGameOver => isGameOver;
    // One rewarded-ad revive per run, and only when an ad is ready to play.
    public bool CanRevive => !reviveUsed && RewardedAds.IsReady;


    public int maxAmmo = 50;
    public int ammo = 10;

    [SerializeField] private ParticleSystem deathParticles;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TrailRenderer trailRenderer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        baseGaugeIncreasePerSecond = gaugeIncreasePerSecond;
        // DontDestroyOnLoad(this.gameObject); // enable if you use multiple scenes
    }

    private void Start()
    {
        bestScore = PlayerPrefs.GetFloat("BestScore", 0f);
        enemySpawner = FindFirstObjectByType<EnemySpawner>();
        if (gameOverPanel != null)
            gameOverScreen = GameOverScreen.AttachTo(gameOverPanel);
        EnsureDrainHeatUI();
        EnsureDeathFeedbackUI();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateScoreUI();
        UpdateEnergyUI();
        UpdateGaugeUI();
    }

    private void OnDisable()
    {
        ResetCameraPosition();
    }

    private void EnsureDrainHeatUI()
    {
        if (drainHeatText != null || drainGaugeFill == null || scoreText == null)
            return;

        Transform parent = drainGaugeFill.transform.parent;
        if (parent == null)
            return;

        drainHeatText = Instantiate(scoreText, parent, false);
        drainHeatText.gameObject.name = "DrainHeatText";
        drainHeatText.text = "DRAIN HEAT 0%";
        drainHeatText.alignment = TextAlignmentOptions.Center;
        drainHeatText.fontSize = 20f;
        drainHeatText.color = drainCoolColor;
        drainHeatText.margin = Vector4.zero;
        drainHeatText.textWrappingMode = TextWrappingModes.NoWrap;
        drainHeatText.overflowMode = TextOverflowModes.Overflow;
        drainHeatText.raycastTarget = false;

        RectTransform rect = drainHeatText.rectTransform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 28f);
        rect.sizeDelta = new Vector2(420f, 34f);
    }

    private void EnsureDeathFeedbackUI()
    {
        if (deathFlash != null || scoreText == null)
            return;

        Canvas canvas = scoreText.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        GameObject flashObject = new GameObject("DeathFlash", typeof(RectTransform), typeof(Image));
        flashObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = flashObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        deathFlash = flashObject.GetComponent<Image>();
        deathFlash.color = new Color(1f, 0.25f, 0.35f, 0f);
        deathFlash.raycastTarget = false;
        flashObject.SetActive(false);
    }

    private void Update()
    {
        if (isGameOver) return;
        HandleDrainGauge();
    }

    private void HandleDrainGauge()
    {
        if (overheated)
        {
            overheatTimer -= Time.deltaTime;
            if (overheatTimer <= 0f)
            {
                overheated = false;
                // restart slightly warm
                drainGauge = drainGaugeMax * 0.3f;
                UpdateGaugeUI();

                if (GameFeedback.Instance != null)
                    GameFeedback.Instance.PlayOverheatRecovered();
            }
            return;
        }

        if (player != null && player.isDraining)
        {
            drainGauge += gaugeIncreasePerSecond * Time.deltaTime;
            if (drainGauge >= drainGaugeMax)
            {
                drainGauge = drainGaugeMax;
                overheated = true;
                overheatTimer = overheatDuration;

                // force stop draining
                player.SetDraining(false);

                if (GameFeedback.Instance != null)
                    GameFeedback.Instance.PlayOverheat();

                if (overheatNovaEnabled)
                    TriggerOverheatNova();
            }
        }
        else
        {
            drainGauge -= gaugeDecreasePerSecond * Time.deltaTime;
            if (drainGauge < 0f) drainGauge = 0f;
        }

        UpdateGaugeUI();
    }

    public void AddEnergy(float amount)
    {
        if (amount <= 0f) return;

        currentEnergy += amount;
        currentScore += amount; // 1:1 mapping for now

        UpdateEnergyUI();
        UpdateScoreUI();
    }

    public void AddScore(int amount)
    {
        currentScore += amount;
        UpdateScoreUI();
    }

    public void CoolDrainHeat(float amount)
    {
        if (amount <= 0f)
            return;

        drainGauge = Mathf.Max(0f, drainGauge - amount);
        overheated = false;
        overheatTimer = 0f;
        UpdateGaugeUI();
    }

    public void MultiplyDrainHeatBuildRate(float multiplier)
    {
        if (multiplier > 0f)
            gaugeIncreasePerSecond *= multiplier;
    }

    public void ResetPowerUpModifiers()
    {
        gaugeIncreasePerSecond = baseGaugeIncreasePerSecond;
        overheatNovaEnabled = false;
    }

    // Hides the heat gauge and its label, e.g. while the power-up cards cover the
    // top of the screen (on phones wider than 16:9 they would overlap).
    public void SetDrainGaugeVisible(bool visible)
    {
        if (drainGaugeFill == null)
            return;

        Transform gauge = drainGaugeFill.transform.parent;
        GameObject target = gauge != null ? gauge.gameObject : drainGaugeFill.gameObject;
        if (target.activeSelf != visible)
            target.SetActive(visible);
    }

    // Rare power-up: overheating releases a shockwave around the player.
    public void EnableOverheatNova()
    {
        overheatNovaEnabled = true;
    }

    private void TriggerOverheatNova()
    {
        if (player == null)
            return;

        Vector2 center = player.transform.position;
        float radiusSq = overheatNovaRadius * overheatNovaRadius;

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (EnemyBase enemy in enemies)
        {
            if (enemy != null && ((Vector2)enemy.transform.position - center).sqrMagnitude <= radiusSq)
                enemy.TakeDamage(overheatNovaDamage);
        }

        // Also wipe enemy shots caught in the blast.
        EnemyProjectile[] projectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        foreach (EnemyProjectile projectile in projectiles)
        {
            if (projectile != null && ((Vector2)projectile.transform.position - center).sqrMagnitude <= radiusSq)
                Destroy(projectile.gameObject);
        }

        if (GameFeedback.Instance != null)
            GameFeedback.Instance.PlayNova(center, overheatNovaRadius, overheatNovaColor, player.VisualRenderer);
    }

    public void PlayDrainHeatRewardFeedback()
    {
        if (drainGaugeFill == null)
            return;

        EnsureDrainHeatRewardSweep();
        if (drainHeatRewardSweep == null)
            return;

        if (drainHeatRewardRoutine != null)
            StopCoroutine(drainHeatRewardRoutine);

        drainHeatRewardRoutine = StartCoroutine(PlayDrainHeatRewardFeedbackRoutine());
    }

    private void EnsureDrainHeatRewardSweep()
    {
        if (drainHeatRewardSweep != null || drainGaugeFill == null || drainGaugeFill.transform.parent == null)
            return;

        GameObject sweepObject = new GameObject("DrainHeatRewardSweep", typeof(RectTransform), typeof(Image));
        sweepObject.transform.SetParent(drainGaugeFill.transform.parent, false);

        drainHeatRewardSweep = sweepObject.GetComponent<Image>();
        drainHeatRewardSweep.sprite = drainGaugeFill.sprite;
        drainHeatRewardSweep.type = Image.Type.Simple;
        drainHeatRewardSweep.raycastTarget = false;
        drainHeatRewardSweep.color = new Color(drainHeatRewardColor.r, drainHeatRewardColor.g, drainHeatRewardColor.b, 0f);
        sweepObject.transform.SetAsLastSibling();

        RectTransform rect = drainHeatRewardSweep.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private System.Collections.IEnumerator PlayDrainHeatRewardFeedbackRoutine()
    {
        RectTransform gaugeRect = drainGaugeFill.rectTransform;
        RectTransform sweepRect = drainHeatRewardSweep.rectTransform;
        float gaugeWidth = gaugeRect.rect.width;
        float sweepWidth = Mathf.Min(drainHeatSweepWidth, gaugeWidth);

        sweepRect.sizeDelta = new Vector2(sweepWidth, gaugeRect.rect.height + 6f);

        float startX = -gaugeWidth * 0.5f - sweepWidth * 0.5f;
        float endX = gaugeWidth * 0.5f + sweepWidth * 0.5f;
        float elapsed = 0f;

        while (elapsed < drainHeatSweepDuration)
        {
            float progress = elapsed / drainHeatSweepDuration;
            sweepRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, progress), 0f);
            float alpha = Mathf.Sin(progress * Mathf.PI) * 0.9f;
            drainHeatRewardSweep.color = new Color(
                drainHeatRewardColor.r,
                drainHeatRewardColor.g,
                drainHeatRewardColor.b,
                alpha);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        drainHeatRewardSweep.color = new Color(
            drainHeatRewardColor.r,
            drainHeatRewardColor.g,
            drainHeatRewardColor.b,
            0f);
        drainHeatRewardRoutine = null;
    }

    public void AddAmmo(int amount)
    {
        ammo = Mathf.Clamp(ammo + amount, 0, maxAmmo);
    }

    public bool SpendAmmo(int cost)
    {
        if (ammo < cost) return false;
        ammo -= cost;
        return true;
    }

    public void GameOver()
    {
        GameOver(DeathCause.Unknown);
    }

    public void GameOver(DeathCause cause)
    {
        if (isGameOver) return;
        isGameOver = true;

        // Read the player's state for the Game Over tip before anything resets it.
        bool wasDraining = player != null && player.isDraining;
        PlayerAmmo playerAmmo = player != null ? player.GetComponent<PlayerAmmo>() : null;
        bool outOfAmmo = playerAmmo != null && !playerAmmo.HasAmmo();

        if (KillStreak.Instance != null)
            KillStreak.Instance.ResetStreak();

        if (DrainableSpawner.Instance != null)
        {
            DrainableSpawner.Instance.StopSpawning();
            DrainableSpawner.Instance.PauseForPowerUpSelection();
        }

        PlayDeathFeedback();

        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            PlayerPrefs.SetFloat("BestScore", bestScore);
            newBestThisRun = true;
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverScreen != null)
        {
            gameOverScreen.Show(new GameOverResult
            {
                score = Mathf.RoundToInt(currentScore),
                bestScore = Mathf.RoundToInt(bestScore),
                // Still the best after a revive, as long as this run holds it.
                newBest = newBestThisRun && currentScore >= bestScore,
                wave = enemySpawner != null ? enemySpawner.CurrentWave : 0,
                kills = RunStats.Kills,
                bestMultiplier = RunStats.BestMultiplier,
                seconds = RunStats.Seconds,
                tip = GameOverScreen.TipFor(cause, outOfAmmo, wasDraining),
                canRevive = CanRevive
            });
        }

        Debug.Log("Game Over");

        SetPlayerVisualVisible(false);
        trailRenderer.enabled = false;
        deathParticles.Play();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + Mathf.RoundToInt(currentScore);
    }

    private void UpdateEnergyUI()
    {
        if (energyText != null)
            energyText.text = "Energy: " + Mathf.RoundToInt(currentEnergy);
    }

    private void UpdateGaugeUI()
    {
        float gaugePercent = drainGaugeMax <= 0f ? 0f : Mathf.Clamp01(drainGauge / drainGaugeMax);

        if (drainGaugeFill != null)
        {
            drainGaugeFill.fillAmount = gaugePercent;
            drainGaugeFill.color = GetDrainGaugeColor(gaugePercent);
        }

        if (drainHeatText != null)
        {
            drainHeatText.text = overheated
                ? $"OVERHEATED — {Mathf.CeilToInt(Mathf.Max(overheatTimer, 0f))}s"
                : $"DRAIN HEAT {Mathf.RoundToInt(gaugePercent * 100f)}%";
            drainHeatText.color = overheated ? drainCriticalColor : GetDrainGaugeColor(gaugePercent);
        }
    }

    private Color GetDrainGaugeColor(float gaugePercent)
    {
        if (overheated)
            return drainCriticalColor;

        const float warmThreshold = 0.65f;
        return gaugePercent <= warmThreshold
            ? Color.Lerp(drainCoolColor, drainWarmColor, gaugePercent / warmThreshold)
            : Color.Lerp(drainWarmColor, drainCriticalColor, (gaugePercent - warmThreshold) / (1f - warmThreshold));
    }

    private void PlayDeathFeedback()
    {
        EnsureDeathFeedbackUI();

        if (deathFeedbackRoutine != null)
            StopCoroutine(deathFeedbackRoutine);

        deathFeedbackRoutine = StartCoroutine(PlayDeathFeedbackRoutine());

#if UNITY_ANDROID || UNITY_IOS
        if (vibrateOnDeath && GameSettings.VibrationOn)
            Handheld.Vibrate();
#endif
    }

    private System.Collections.IEnumerator PlayDeathFeedbackRoutine()
    {
        if (deathFlash != null)
        {
            deathFlash.gameObject.SetActive(true);
            deathFlash.transform.SetAsLastSibling();
        }

        // Render-only shake (GameFeedback) instead of moving the camera, so it
        // can't fight the follow camera. Same peak strength, similar length.
        if (GameFeedback.Instance != null)
            GameFeedback.Instance.AddShake(deathShakeStrength / 0.45f);

        float totalDuration = Mathf.Max(deathFlashDuration, deathShakeDuration);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            if (deathFlash != null)
            {
                float flashProgress = deathFlashDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / deathFlashDuration);
                deathFlash.color = new Color(1f, 0.25f, 0.35f, Mathf.Lerp(deathFlashOpacity, 0f, flashProgress));
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ResetCameraPosition();

        if (deathFlash != null)
            deathFlash.gameObject.SetActive(false);

        deathFeedbackRoutine = null;
    }

    private void ResetCameraPosition()
    {
        if (shakenCamera == null)
            return;

        shakenCamera.localPosition = cameraBaseLocalPosition;
        shakenCamera = null;
    }

    public void ResetGame()
    {
        if (KillStreak.Instance != null)
            KillStreak.Instance.ResetStreak();

        StopTransientFeedback();
        ResetCameraPosition();
        isGameOver = false;
        reviveUsed = false;
        newBestThisRun = false;
        drainGauge = 0f;
        overheated = false;
        currentEnergy = 0f;
        currentScore = 0f;
        SetPlayerVisualVisible(true);
        trailRenderer.enabled = true;
        deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateEnergyUI();
        UpdateScoreUI();
        UpdateGaugeUI();

        ResetPlayerForNewRun();
        DestroyAllEnemies();
        DestroyAllProjectiles();
        if (DrainableSpawner.Instance != null)
            DrainableSpawner.Instance.ResetForNewRun();
    }

    public void RestartRun()
    {
        ResetGame();

        if (enemySpawner != null)
            enemySpawner.StartSpawning();
    }

    private void ResetPlayerForNewRun()
    {
        if (player != null)
        {
            player.ResetForNewRun();

            PlayerAmmo playerAmmo = player.GetComponent<PlayerAmmo>();
            if (playerAmmo != null)
                playerAmmo.ResetForNewRun();

            PlayerShooter playerShooter = player.GetComponent<PlayerShooter>();
            if (playerShooter != null)
                playerShooter.ResetForNewRun();
        }

        // The player is back at the start; bring the follow camera with them.
        if (CameraFollow.Instance != null)
            CameraFollow.Instance.SnapToTarget();

        DrainZone drainZone = FindFirstObjectByType<DrainZone>();
        if (drainZone != null)
            drainZone.ResetForNewRun();
    }

    public void RevivePlayer()
    {
        if (!isGameOver)
            return;

        if (KillStreak.Instance != null)
            KillStreak.Instance.ResetStreak();

        StopTransientFeedback();
        ResetCameraPosition();
        isGameOver = false;
        reviveUsed = true;

        // Come back with a clean slate: no leftover heat, enemies, or shots in flight.
        drainGauge = 0f;
        overheated = false;
        overheatTimer = 0f;
        UpdateGaugeUI();

        SetPlayerVisualVisible(true);
        trailRenderer.enabled = true;
        deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (player != null)
            player.SetDraining(false);

        DestroyAllEnemies();
        DestroyAllProjectiles();

        // GameOver froze the drainables; the wave system decides how the run resumes.
        if (enemySpawner != null)
            enemySpawner.OnPlayerRevived();
        else if (DrainableSpawner.Instance != null)
            DrainableSpawner.Instance.StartSpawning();
    }

    public void DestroyAllEnemies()
    {
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (EnemyBase enemy in enemies)
        {
            Destroy(enemy.gameObject);
        }
    }

    private void DestroyAllProjectiles()
    {
        PlayerBullet[] bullets = FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None);
        foreach (PlayerBullet bullet in bullets)
        {
            if (bullet != null)
                Destroy(bullet.gameObject);
        }

        EnemyProjectile[] enemyProjectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        foreach (EnemyProjectile projectile in enemyProjectiles)
        {
            if (projectile != null)
                Destroy(projectile.gameObject);
        }
    }

    private void StopTransientFeedback()
    {
        if (deathFeedbackRoutine != null)
            StopCoroutine(deathFeedbackRoutine);

        deathFeedbackRoutine = null;

        if (deathFlash != null)
        {
            deathFlash.color = new Color(1f, 0.25f, 0.35f, 0f);
            deathFlash.gameObject.SetActive(false);
        }

        if (drainHeatRewardRoutine != null)
            StopCoroutine(drainHeatRewardRoutine);

        drainHeatRewardRoutine = null;

        if (drainHeatRewardSweep != null)
        {
            drainHeatRewardSweep.color = new Color(
                drainHeatRewardColor.r,
                drainHeatRewardColor.g,
                drainHeatRewardColor.b,
                0f);
        }
    }

    // Revive button: plays a rewarded ad first (see GameOverScreen.RequestRevive).
    public void OnClickRevive()
    {
        if (gameOverScreen != null)
            gameOverScreen.RequestRevive();
    }

    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
    }

    private void SetPlayerVisualVisible(bool visible)
    {
        if (player != null && player.VisualRenderer != null)
        {
            player.VisualRenderer.enabled = visible;
            return;
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
    }
    
}
