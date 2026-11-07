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
    public TMP_Text gameOverScoreText;
    public TMP_Text bestScoreText;

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

    public bool IsOverheated => overheated;
    public bool IsGameOver => isGameOver;


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
        EnsureGameOverResultsUI();
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

    private void EnsureGameOverResultsUI()
    {
        bool hasDistinctResultFields =
            gameOverScoreText != null &&
            bestScoreText != null &&
            gameOverScoreText != scoreText &&
            bestScoreText != scoreText &&
            gameOverScoreText != bestScoreText;

        if (hasDistinctResultFields || gameOverPanel == null || scoreText == null)
            return;

        // The original scene reused ScoreText for every result field. Create
        // dedicated labels at runtime so the in-game score cannot be overwritten
        // when the end screen displays the final score and best score.
        Transform parent = gameOverPanel.transform.childCount > 0
            ? gameOverPanel.transform.GetChild(0)
            : gameOverPanel.transform;

        CreateGameOverLabel("GameOverTitle", "GAME OVER", 250f, 52f, new Color(1f, 0.25f, 0.4f));
        gameOverScoreText = CreateGameOverLabel("FinalScoreText", "Score: 0", 150f, 34f, Color.white);
        bestScoreText = CreateGameOverLabel("BestScoreText", "Best: 0", 80f, 28f, new Color(0.85f, 0.85f, 0.9f));

        TMP_Text CreateGameOverLabel(string objectName, string initialText, float yPosition, float fontSize, Color color)
        {
            TMP_Text label = Instantiate(scoreText, parent, false);
            label.gameObject.name = objectName;
            label.text = initialText;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.color = color;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            RectTransform rect = label.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, yPosition);
            rect.sizeDelta = new Vector2(1200f, 90f);

            return label;
        }
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
        if (isGameOver) return;
        isGameOver = true;

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
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverScoreText != null) gameOverScoreText.text = "Score: " + Mathf.RoundToInt(currentScore);
        if (bestScoreText != null) bestScoreText.text = "Best: " + Mathf.RoundToInt(bestScore);

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
        if (vibrateOnDeath)
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

        Camera gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            shakenCamera = gameplayCamera.transform;
            cameraBaseLocalPosition = shakenCamera.localPosition;
        }

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

            if (shakenCamera != null && elapsed < deathShakeDuration)
            {
                float shakeFalloff = 1f - Mathf.Clamp01(elapsed / deathShakeDuration);
                Vector2 offset = Random.insideUnitCircle * deathShakeStrength * shakeFalloff;
                shakenCamera.localPosition = cameraBaseLocalPosition + new Vector3(offset.x, offset.y, 0f);
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
        StopTransientFeedback();
        ResetCameraPosition();
        isGameOver = false;
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

        EnemySpawner enemySpawner = FindFirstObjectByType<EnemySpawner>();
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

        DrainZone drainZone = FindFirstObjectByType<DrainZone>();
        if (drainZone != null)
            drainZone.ResetForNewRun();
    }

    public void RevivePlayer()
    {
        if (!isGameOver)
            return;

        StopTransientFeedback();
        ResetCameraPosition();
        isGameOver = false;

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
        EnemySpawner enemySpawner = FindFirstObjectByType<EnemySpawner>();
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

    public void OnClickRevive()
    {
        RevivePlayer();
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
