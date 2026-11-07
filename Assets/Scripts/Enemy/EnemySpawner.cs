using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyOption
    {
        public EnemyBase prefab;

        // Kept so the existing scene data remains intact. Waves now control
        // how many enemies spawn and when they appear.
        [HideInInspector] public float startInterval = 2f;
        [HideInInspector] public float minInterval = 0.6f;
        [HideInInspector] public float intervalDecreasePerMinute = 0.5f;
        [HideInInspector] public float spawnChance = 1f;
        [HideInInspector] public int maxAlive = 999;
    }

    private enum WaveState
    {
        Idle,
        WaveIntro,
        Spawning,
        Intermission,
        PowerUpSelection,
        PowerUpOutro
    }

    private enum FirstWaveCameraState
    {
        None,
        ZoomingIn,
        Holding,
        ZoomingOut
    }

    private enum PowerUpCameraState
    {
        None,
        ZoomingIn,
        Holding,
        ZoomingOut
    }

    private enum CommonPowerUp
    {
        RapidFire,
        PowerRounds,
        KineticDrives,
        ThermalVent
    }

    [Header("Enemy Types")]
    public EnemyOption[] enemyOptions;

    [Header("Wave Timing")]
    [SerializeField, Min(0f)] private float firstWaveDelay = 3f;
    [SerializeField, Min(0.05f)] private float enemySpawnInterval = 0.45f;
    // No between-wave intermission setting: the power-up card selection is the
    // breather, and the next wave's title intro plays before spawning starts.
    [Tooltip("Breather before the interrupted wave replays after a revive.")]
    [SerializeField, Min(0f)] private float reviveRecoveryDelay = 2f;

    [Header("Wave Spawn Distance")]
    [SerializeField, Min(0f)] private float firstWaveSpawnDistance = 12f;
    [SerializeField, Min(0f)] private float spawnDistanceDecreasePerWave = 0.75f;
    [SerializeField, Min(0f)] private float minimumSpawnDistance = 7f;

    [Header("Wave Rewards")]
    [SerializeField, Min(0)] private int ammoRewardPerWave = 5;
    [SerializeField, Min(0f)] private float drainHeatReductionPerWave = 30f;

    [Header("Wave Reward Visuals")]
    [SerializeField] private Color waveRewardColor = new Color(0.7f, 1f, 1f, 1f);
    [SerializeField, Range(3, 64)] private int waveRewardRingSegments = 40;
    [SerializeField, Min(0f)] private float waveRewardRingStartRadius = 0.55f;
    [SerializeField, Min(0f)] private float waveRewardRingEndRadius = 2.3f;
    [SerializeField, Min(0.01f)] private float waveRewardRingDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float waveRewardRingWidth = 0.055f;
    [SerializeField, Min(0f)] private float ammoShardSpawnRadius = 2.15f;
    [SerializeField, Min(0f)] private float ammoShardTravelSpeed = 6.5f;
    [SerializeField, Min(0.01f)] private float ammoShardLifetime = 0.55f;

    [Header("Wave UI")]
    [SerializeField] private TMP_Text waveStatusText;
    [SerializeField, Min(0f)] private float waveTitleCenterHoldDuration = 0.65f;
    [SerializeField, Min(0.01f)] private float waveTitleMoveDuration = 0.55f;
    [SerializeField, Min(1f)] private float waveTitleFontSize = 64f;
    [SerializeField, Min(1f)] private float waveStatusFontSize = 24f;
    [SerializeField] private int waveStatusSortingOrder = 100;

    [Header("First Wave Camera Intro")]
    [SerializeField, Range(0.1f, 1f)] private float firstWaveZoomMultiplier = 0.45f;
    [SerializeField, Min(0.01f)] private float firstWaveZoomInDuration = 0.55f;

    [Header("Common Power-Up Cards")]
    [SerializeField, Range(0.1f, 1f)] private float powerUpZoomMultiplier = 0.45f;
    [SerializeField, Min(0.01f)] private float powerUpZoomInDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float powerUpZoomOutDuration = 0.45f;
    [SerializeField] private Color commonCardColor = new Color(0.08f, 0.19f, 0.19f, 0.97f);
    [SerializeField] private Color commonCardAccentColor = new Color(0.55f, 1f, 0.8f, 1f);
    [SerializeField] private Color commonCardTextColor = new Color(0.9f, 1f, 0.96f, 1f);
    // Buffs are > 1 (stacking multiplicatively per pick); Thermal Vent scales heat build-up, so it's < 1.
    // Power Rounds breakpoints at base damage 20: 1 pick -> shooters (45 HP) die in 2 shots,
    // 2 picks -> dashers (30 HP) die in 1. Chasers (20 HP) already die in 1.
    [SerializeField, Range(1f, 2f)] private float rapidFireMultiplier = 1.25f;
    [SerializeField, Range(1f, 2f)] private float powerRoundsMultiplier = 1.25f;
    [SerializeField, Range(1f, 2f)] private float kineticDrivesMultiplier = 1.15f;
    [SerializeField, Range(0.5f, 1f)] private float thermalVentMultiplier = 0.8f;

    private readonly List<EnemyBase> spawnQueue = new List<EnemyBase>();
    private readonly HashSet<EnemyBase> activeWaveEnemies = new HashSet<EnemyBase>();

    private WaveState waveState = WaveState.Idle;
    private bool gameStarted;
    private int waveNumber;
    private float nextSpawnTimer;
    private float intermissionTimer;
    private float waveIntroTimer;
    private bool firstWaveCameraIntroActive;
    private FirstWaveCameraState firstWaveCameraState;
    private float firstWaveCameraAnimationTimer;
    private Camera firstWaveCamera;
    private Vector3 firstWaveCameraBasePosition;
    private float firstWaveCameraBaseSize;
    private Vector3 firstWaveCameraFocusedPosition;
    private bool powerUpCardsVisible;
    private GameObject powerUpCardPanel;
    private PowerUpCameraState powerUpCameraState;
    private float powerUpCameraAnimationTimer;
    private Camera powerUpCamera;
    private Vector3 powerUpCameraBasePosition;
    private Vector3 powerUpCameraFocusedPosition;
    private float powerUpCameraBaseSize;

    private void Update()
    {
        GameManager gameManager = GameManager.Instance;
        bool isGameOver = gameManager != null && gameManager.IsGameOver;

        // Power-up cards sort above the Game Over panel, so hide them while the
        // player is dead (they would cover Revive/Restart) and bring them back on revive.
        if (powerUpCardPanel != null && powerUpCardPanel.activeSelf == isGameOver)
            powerUpCardPanel.SetActive(!isGameOver);

        // Restart and Menu reset the waves explicitly (StartSpawning / StopSpawning);
        // Revive resumes through OnPlayerRevived. Nothing to do while dead.
        if (isGameOver)
            return;

        if (!gameStarted)
            return;

        EnsureWaveStatusUI();
        RemoveDefeatedWaveEnemies();

        switch (waveState)
        {
            case WaveState.Idle:
                BeginIntermission(firstWaveDelay, false);
                break;

            case WaveState.Intermission:
                intermissionTimer -= Time.deltaTime;
                if (intermissionTimer <= 0f)
                    BeginNextWave();
                break;

            case WaveState.WaveIntro:
                waveIntroTimer -= Time.deltaTime;
                if (waveIntroTimer <= waveTitleMoveDuration)
                    BeginFirstWaveCameraZoomOut();

                if (waveIntroTimer <= 0f)
                {
                    CompleteFirstWaveCameraIntro();
                    waveState = WaveState.Spawning;
                    nextSpawnTimer = 0f;
                }
                break;

            case WaveState.Spawning:
                SpawnWaveEnemies();
                if (spawnQueue.Count == 0 && activeWaveEnemies.Count == 0)
                    CompleteWave();
                break;

            case WaveState.PowerUpSelection:
                UpdatePowerUpCamera();
                if (!powerUpCardsVisible &&
                    (!IsPowerUpCameraActive() || powerUpCameraState == PowerUpCameraState.Holding))
                {
                    if (!ShowCommonPowerUpCards())
                    {
                        BeginPowerUpCameraZoomOut();
                        waveState = WaveState.PowerUpOutro;
                    }
                }
                break;

            case WaveState.PowerUpOutro:
                UpdatePowerUpCamera();
                if (!IsPowerUpCameraActive())
                {
                    if (DrainableSpawner.Instance != null)
                        DrainableSpawner.Instance.ResumeAfterPowerUpSelection();

                    BeginNextWave();
                }
                break;
        }

        UpdateFirstWaveCameraIntro();
        UpdateWaveStatusUI();
    }

    public void StartSpawning()
    {
        gameStarted = true;
        ResetWaves();
        if (DrainableSpawner.Instance != null)
            DrainableSpawner.Instance.StartSpawning();
        BeginFirstWaveCameraIntro();
    }

    public void StopSpawning()
    {
        gameStarted = false;
        waveState = WaveState.Idle;
        ResetFirstWaveCameraIntro();
        ResetPowerUpCamera();
        HideCommonPowerUpCards();
        ResetRunPowerUps();
        if (DrainableSpawner.Instance != null)
            DrainableSpawner.Instance.StopSpawning();

        if (waveStatusText != null)
            waveStatusText.text = string.Empty;
    }

    // Called by GameManager after it has cleared enemies and projectiles for a revive.
    // Keeps the run's wave number and power-ups instead of starting over.
    public void OnPlayerRevived()
    {
        if (!gameStarted)
            return;

        bool inPowerUpSelection =
            waveState == WaveState.PowerUpSelection ||
            waveState == WaveState.PowerUpOutro;

        if (waveState == WaveState.WaveIntro || waveState == WaveState.Spawning)
        {
            // The wave's enemies were wiped by the revive. Replay the same wave
            // rather than letting the empty queue count as a clear (free reward).
            ResetFirstWaveCameraIntro();
            spawnQueue.Clear();
            activeWaveEnemies.Clear();
            waveNumber = Mathf.Max(0, waveNumber - 1);
            BeginIntermission(reviveRecoveryDelay, false);
        }

        // Power-up selection keeps drainables paused until a card is picked;
        // every other state needs them running again so the player can drain.
        if (!inPowerUpSelection && DrainableSpawner.Instance != null)
            DrainableSpawner.Instance.StartSpawning();
    }

    private void ResetWaves()
    {
        ResetFirstWaveCameraIntro();
        ResetPowerUpCamera();
        HideCommonPowerUpCards();
        ResetRunPowerUps();
        spawnQueue.Clear();
        activeWaveEnemies.Clear();
        waveNumber = 0;
        waveIntroTimer = 0f;
        BeginIntermission(firstWaveDelay, false);
    }

    private void BeginIntermission(float duration, bool rewardPlayer)
    {
        waveState = WaveState.Intermission;
        intermissionTimer = duration;

        if (rewardPlayer)
            GiveWaveReward();
    }

    private void BeginNextWave()
    {
        waveNumber++;
        BuildWaveQueue(waveNumber);
        waveState = WaveState.WaveIntro;
        waveIntroTimer = waveTitleCenterHoldDuration + waveTitleMoveDuration;

        if (GameFeedback.Instance != null)
            GameFeedback.Instance.PlayWaveStart();
    }

    private void BuildWaveQueue(int wave)
    {
        spawnQueue.Clear();

        EnemyBase chaser = GetEnemyPrefab<EnemyChaser>();
        EnemyBase dasher = GetEnemyPrefab<EnemyDasher>();
        EnemyBase shooter = GetEnemyPrefab<EnemyShooter>();

        if (wave == 1)
        {
            AddToSpawnQueue(chaser, 5);
        }
        else if (wave == 2)
        {
            AddToSpawnQueue(chaser, 7);
        }
        else if (wave == 3)
        {
            AddToSpawnQueue(chaser, 6);
            AddToSpawnQueue(dasher, 2);
        }
        else
        {
            int wavesAfterFour = wave - 4;
            AddToSpawnQueue(chaser, 8 + wavesAfterFour * 2);
            AddToSpawnQueue(dasher, 3 + wavesAfterFour);
            AddToSpawnQueue(shooter, 1 + wavesAfterFour / 2);
        }

        ShuffleSpawnQueue();
    }

    private void AddToSpawnQueue(EnemyBase prefab, int amount)
    {
        if (prefab == null)
            return;

        for (int i = 0; i < amount; i++)
            spawnQueue.Add(prefab);
    }

    private void ShuffleSpawnQueue()
    {
        for (int i = spawnQueue.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            EnemyBase temporary = spawnQueue[i];
            spawnQueue[i] = spawnQueue[randomIndex];
            spawnQueue[randomIndex] = temporary;
        }
    }

    private void SpawnWaveEnemies()
    {
        nextSpawnTimer -= Time.deltaTime;
        if (nextSpawnTimer > 0f || spawnQueue.Count == 0)
            return;

        EnemyBase prefab = spawnQueue[0];
        spawnQueue.RemoveAt(0);

        Vector3 center = GameManager.Instance != null && GameManager.Instance.player != null
            ? GameManager.Instance.player.transform.position
            : Vector3.zero;
        EnemyBase enemy = Instantiate(prefab, GetSpawnPosition(center), Quaternion.identity);
        activeWaveEnemies.Add(enemy);
        nextSpawnTimer = enemySpawnInterval;
    }

    private Vector3 GetSpawnPosition(Vector3 center)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * CurrentSpawnDistance();
        return center + offset;
    }

    private float CurrentSpawnDistance()
    {
        float distanceReduction = Mathf.Max(0, waveNumber - 1) * spawnDistanceDecreasePerWave;
        return Mathf.Max(minimumSpawnDistance, firstWaveSpawnDistance - distanceReduction);
    }

    private void RemoveDefeatedWaveEnemies()
    {
        activeWaveEnemies.RemoveWhere(enemy => enemy == null);
    }

    private void CompleteWave()
    {
        if (GameFeedback.Instance != null)
            GameFeedback.Instance.PlayWaveClear();

        GiveWaveReward();
        BeginPowerUpSelection();
    }

    private void GiveWaveReward()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        PlayerAmmo ammo = gameManager.player != null ? gameManager.player.GetComponent<PlayerAmmo>() : null;
        int ammoGranted = 0;
        if (ammo != null && ammoRewardPerWave > 0)
        {
            int ammoBeforeReward = ammo.currentAmmo;
            ammo.Add(ammoRewardPerWave);
            ammoGranted = ammo.currentAmmo - ammoBeforeReward;

            if (ammoGranted > 0)
                ammo.PlayAmmoRewardFeedback();
        }

        float heatRemoved = Mathf.Min(drainHeatReductionPerWave, gameManager.drainGauge);
        if (heatRemoved > 0f)
            gameManager.PlayDrainHeatRewardFeedback();

        gameManager.CoolDrainHeat(drainHeatReductionPerWave);
        PlayWaveRewardBurst(gameManager.player, ammoGranted);
    }

    private void PlayWaveRewardBurst(PlayerMovement2D player, int ammoGranted)
    {
        if (player == null)
            return;

        CreateWaveRewardRing(player);

        if (ammoGranted > 0)
            CreateAmmoRewardShards(player, ammoGranted);
    }

    private void CreateWaveRewardRing(PlayerMovement2D player)
    {
        GameObject ringObject = new GameObject("WaveRewardRing");
        ringObject.transform.SetParent(player.transform, false);

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = waveRewardRingSegments;
        ring.widthMultiplier = waveRewardRingWidth;
        ring.material = player.VisualRenderer != null ? player.VisualRenderer.sharedMaterial : null;
        ring.sortingLayerID = player.VisualRenderer != null ? player.VisualRenderer.sortingLayerID : 0;
        ring.sortingOrder = player.VisualRenderer != null ? player.VisualRenderer.sortingOrder + 3 : 3;

        StartCoroutine(AnimateWaveRewardRing(ring));
    }

    private System.Collections.IEnumerator AnimateWaveRewardRing(LineRenderer ring)
    {
        float elapsed = 0f;

        while (elapsed < waveRewardRingDuration)
        {
            if (ring == null)
                yield break;

            float progress = elapsed / waveRewardRingDuration;
            float radius = Mathf.Lerp(waveRewardRingStartRadius, waveRewardRingEndRadius, Mathf.SmoothStep(0f, 1f, progress));
            Color ringColor = new Color(waveRewardColor.r, waveRewardColor.g, waveRewardColor.b, 1f - progress);

            ring.startColor = ringColor;
            ring.endColor = ringColor;

            for (int index = 0; index < waveRewardRingSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / waveRewardRingSegments;
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ring != null)
            Destroy(ring.gameObject);
    }

    private void CreateAmmoRewardShards(PlayerMovement2D player, int ammoGranted)
    {
        GameObject shardObject = new GameObject("WaveAmmoShards");
        shardObject.transform.SetParent(player.transform, false);

        ParticleSystem shards = shardObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = shards.main;
        main.duration = ammoShardLifetime;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = ammoShardLifetime;
        main.startSpeed = -ammoShardTravelSpeed;
        main.startSize3D = true;
        main.startSizeX = 0.09f;
        main.startSizeY = 0.24f;
        main.startSizeZ = 1f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = waveRewardColor;
        main.maxParticles = ammoGranted;

        ParticleSystem.EmissionModule emission = shards.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)ammoGranted) });

        ParticleSystem.ShapeModule shape = shards.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = ammoShardSpawnRadius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        shape.alignToDirection = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = shards.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(waveRewardColor, 0.65f),
                new GradientColorKey(waveRewardColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        ParticleSystemRenderer shardRenderer = shards.GetComponent<ParticleSystemRenderer>();
        if (shardRenderer != null && player.VisualRenderer != null)
        {
            shardRenderer.material = player.VisualRenderer.sharedMaterial;
            shardRenderer.sortingLayerID = player.VisualRenderer.sortingLayerID;
            shardRenderer.sortingOrder = player.VisualRenderer.sortingOrder + 4;
        }

        shards.Play();
        Destroy(shardObject, ammoShardLifetime + 0.1f);
    }

    private void BeginPowerUpSelection()
    {
        waveState = WaveState.PowerUpSelection;
        powerUpCardsVisible = false;

        if (waveStatusText != null)
            waveStatusText.text = string.Empty;

        GameManager gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.player != null)
            gameManager.player.SetDraining(false);

        if (DrainableSpawner.Instance != null)
            DrainableSpawner.Instance.PauseForPowerUpSelection();

        BeginPowerUpCameraZoomIn();
    }

    private void BeginPowerUpCameraZoomIn()
    {
        GameManager gameManager = GameManager.Instance;
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null || !gameplayCamera.orthographic || gameManager == null || gameManager.player == null)
            return;

        powerUpCamera = gameplayCamera;
        powerUpCameraBasePosition = gameplayCamera.transform.position;
        powerUpCameraBaseSize = gameplayCamera.orthographicSize;
        Vector3 playerPosition = gameManager.player.transform.position;
        powerUpCameraFocusedPosition = new Vector3(playerPosition.x, playerPosition.y, powerUpCameraBasePosition.z);
        powerUpCameraState = PowerUpCameraState.ZoomingIn;
        powerUpCameraAnimationTimer = 0f;
    }

    private void UpdatePowerUpCamera()
    {
        if (powerUpCameraState == PowerUpCameraState.None)
            return;

        if (powerUpCamera == null)
        {
            powerUpCameraState = PowerUpCameraState.None;
            return;
        }

        if (powerUpCameraState == PowerUpCameraState.Holding)
            return;

        powerUpCameraAnimationTimer += Time.deltaTime;
        bool zoomingIn = powerUpCameraState == PowerUpCameraState.ZoomingIn;
        float duration = zoomingIn ? powerUpZoomInDuration : powerUpZoomOutDuration;
        float progress = Mathf.Clamp01(powerUpCameraAnimationTimer / duration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        powerUpCamera.transform.position = zoomingIn
            ? Vector3.Lerp(powerUpCameraBasePosition, powerUpCameraFocusedPosition, easedProgress)
            : Vector3.Lerp(powerUpCameraFocusedPosition, powerUpCameraBasePosition, easedProgress);
        powerUpCamera.orthographicSize = zoomingIn
            ? Mathf.Lerp(powerUpCameraBaseSize, powerUpCameraBaseSize * powerUpZoomMultiplier, easedProgress)
            : Mathf.Lerp(powerUpCameraBaseSize * powerUpZoomMultiplier, powerUpCameraBaseSize, easedProgress);

        if (progress < 1f)
            return;

        if (zoomingIn)
        {
            powerUpCameraState = PowerUpCameraState.Holding;
            powerUpCameraAnimationTimer = 0f;
        }
        else
        {
            CompletePowerUpCamera();
        }
    }

    private bool IsPowerUpCameraActive()
    {
        return powerUpCameraState != PowerUpCameraState.None;
    }

    private void BeginPowerUpCameraZoomOut()
    {
        if (powerUpCameraState != PowerUpCameraState.Holding)
            return;

        powerUpCameraState = PowerUpCameraState.ZoomingOut;
        powerUpCameraAnimationTimer = 0f;
    }

    private void CompletePowerUpCamera()
    {
        if (powerUpCamera != null)
        {
            powerUpCamera.transform.position = powerUpCameraBasePosition;
            powerUpCamera.orthographicSize = powerUpCameraBaseSize;
        }

        powerUpCameraState = PowerUpCameraState.None;
        powerUpCameraAnimationTimer = 0f;
        powerUpCamera = null;
    }

    private void ResetPowerUpCamera()
    {
        CompletePowerUpCamera();
    }

    private bool ShowCommonPowerUpCards()
    {
        if (powerUpCardsVisible)
            return true;

        GameManager gameManager = GameManager.Instance;
        TMP_Text textSource = gameManager != null ? gameManager.scoreText : null;
        Canvas rootCanvas = textSource != null ? textSource.GetComponentInParent<Canvas>() : null;
        if (rootCanvas == null)
            return false;

        powerUpCardPanel = new GameObject("CommonPowerUpCards", typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(GraphicRaycaster));
        powerUpCardPanel.transform.SetParent(rootCanvas.transform, false);
        powerUpCardPanel.transform.SetAsLastSibling();

        RectTransform panelRect = powerUpCardPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Canvas panelCanvas = powerUpCardPanel.GetComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = waveStatusSortingOrder + 100;

        Image panelBackground = powerUpCardPanel.GetComponent<Image>();
        panelBackground.color = new Color(0.01f, 0.03f, 0.05f, 0.32f);
        panelBackground.raycastTarget = false;

        CreatePowerUpLabel(textSource, powerUpCardPanel.transform, "CHOOSE A POWER-UP", 42f, Color.white, new Vector2(0f, 365f), new Vector2(1200f, 70f));

        List<CommonPowerUp> choices = BuildCommonPowerUpChoices();
        for (int index = 0; index < choices.Count; index++)
        {
            CommonPowerUp choice = choices[index];
            CreatePowerUpCard(textSource, powerUpCardPanel.transform, choice, (index - 1) * 420f);
        }

        powerUpCardsVisible = true;
        return true;
    }

    private List<CommonPowerUp> BuildCommonPowerUpChoices()
    {
        List<CommonPowerUp> choices = new List<CommonPowerUp>
        {
            CommonPowerUp.RapidFire,
            CommonPowerUp.PowerRounds,
            CommonPowerUp.KineticDrives,
            CommonPowerUp.ThermalVent
        };

        for (int index = choices.Count - 1; index > 0; index--)
        {
            int randomIndex = Random.Range(0, index + 1);
            CommonPowerUp temporary = choices[index];
            choices[index] = choices[randomIndex];
            choices[randomIndex] = temporary;
        }

        choices.RemoveRange(3, choices.Count - 3);
        return choices;
    }

    private void CreatePowerUpCard(TMP_Text textSource, Transform parent, CommonPowerUp powerUp, float xPosition)
    {
        GameObject cardObject = new GameObject($"{powerUp}Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        cardObject.transform.SetParent(parent, false);

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(xPosition, -10f);
        cardRect.sizeDelta = new Vector2(360f, 560f);

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = commonCardColor;
        cardImage.raycastTarget = true;

        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = commonCardAccentColor;
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = cardObject.GetComponent<Button>();
        button.targetGraphic = cardImage;
        ColorBlock buttonColors = button.colors;
        buttonColors.normalColor = Color.white;
        buttonColors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        buttonColors.pressedColor = new Color(0.75f, 0.92f, 0.85f, 1f);
        buttonColors.selectedColor = buttonColors.highlightedColor;
        button.colors = buttonColors;

        CreatePowerUpBadge(textSource, cardObject.transform);
        CreateIconDivider(cardObject.transform, new Vector2(0f, 176f), new Vector2(264f, 3f));
        CreatePowerUpIcon(cardObject.transform, powerUp);
        CreatePowerUpLabel(textSource, cardObject.transform, GetPowerUpTitle(powerUp), 30f, commonCardTextColor, new Vector2(0f, -32f), new Vector2(320f, 60f));
        CreatePowerUpStatChip(textSource, cardObject.transform, GetPowerUpDescription(powerUp));
        CreatePowerUpLabel(textSource, cardObject.transform, "TAP TO SELECT", 20f, commonCardAccentColor, new Vector2(0f, -226f), new Vector2(300f, 42f));

        button.onClick.AddListener(() => SelectCommonPowerUp(powerUp));
    }

    private void CreatePowerUpBadge(TMP_Text textSource, Transform parent)
    {
        RectTransform badgeRect = CreateCardElement(parent, "CommonBadge", new Vector2(0f, 220f), new Vector2(150f, 40f));
        Image badgeImage = badgeRect.gameObject.AddComponent<Image>();
        badgeImage.color = new Color(commonCardAccentColor.r, commonCardAccentColor.g, commonCardAccentColor.b, 0.16f);
        badgeImage.raycastTarget = false;

        Outline badgeOutline = badgeRect.gameObject.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(commonCardAccentColor.r, commonCardAccentColor.g, commonCardAccentColor.b, 0.7f);
        badgeOutline.effectDistance = new Vector2(1.5f, -1.5f);

        CreatePowerUpLabel(textSource, badgeRect, "COMMON", 19f, commonCardAccentColor, Vector2.zero, new Vector2(140f, 32f));
    }

    private void CreateIconDivider(Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform divider = CreateCardElement(parent, "IconDivider", position, size);
        Image dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = new Color(commonCardAccentColor.r, commonCardAccentColor.g, commonCardAccentColor.b, 0.48f);
        dividerImage.raycastTarget = false;
    }

    private void CreatePowerUpStatChip(TMP_Text textSource, Transform parent, string text)
    {
        RectTransform chipRect = CreateCardElement(parent, "PowerUpStatChip", new Vector2(0f, -112f), new Vector2(304f, 64f));
        Image chipImage = chipRect.gameObject.AddComponent<Image>();
        chipImage.color = new Color(0f, 0f, 0f, 0.22f);
        chipImage.raycastTarget = false;

        Outline chipOutline = chipRect.gameObject.AddComponent<Outline>();
        chipOutline.effectColor = new Color(commonCardAccentColor.r, commonCardAccentColor.g, commonCardAccentColor.b, 0.42f);
        chipOutline.effectDistance = new Vector2(1.5f, -1.5f);

        CreatePowerUpLabel(textSource, chipRect, text, 21f, Color.white, Vector2.zero, new Vector2(288f, 54f));
    }

    private void CreatePowerUpIcon(Transform parent, CommonPowerUp powerUp)
    {
        Color iconColor = GetPowerUpIconColor(powerUp);
        RectTransform iconRoot = CreateCardElement(parent, $"{powerUp}Icon", new Vector2(0f, 78f), new Vector2(142f, 142f));

        Image iconBackdrop = iconRoot.gameObject.AddComponent<Image>();
        iconBackdrop.color = new Color(iconColor.r, iconColor.g, iconColor.b, 0.12f);
        iconBackdrop.raycastTarget = false;

        Outline iconOutline = iconRoot.gameObject.AddComponent<Outline>();
        iconOutline.effectColor = new Color(iconColor.r, iconColor.g, iconColor.b, 0.62f);
        iconOutline.effectDistance = new Vector2(2f, -2f);

        switch (powerUp)
        {
            case CommonPowerUp.RapidFire:
                CreateRapidFireIcon(iconRoot, iconColor);
                break;

            case CommonPowerUp.PowerRounds:
                CreatePowerRoundsIcon(iconRoot, iconColor);
                break;

            case CommonPowerUp.KineticDrives:
                CreateKineticDrivesIcon(iconRoot, iconColor);
                break;

            case CommonPowerUp.ThermalVent:
                CreateThermalVentIcon(iconRoot, iconColor);
                break;
        }
    }

    private void CreateRapidFireIcon(Transform parent, Color color)
    {
        CreateIconShape(parent, "StreakTop", new Vector2(-9f, 26f), new Vector2(82f, 9f), color, 0f);
        CreateIconShape(parent, "StreakMiddle", new Vector2(4f, 0f), new Vector2(104f, 12f), color, 0f);
        CreateIconShape(parent, "StreakBottom", new Vector2(-9f, -26f), new Vector2(82f, 9f), color, 0f);
        CreateIconShape(parent, "BoltHead", new Vector2(45f, 0f), new Vector2(26f, 26f), color, 45f);
    }

    private void CreatePowerRoundsIcon(Transform parent, Color color)
    {
        CreateIconShape(parent, "RoundCore", Vector2.zero, new Vector2(46f, 46f), color, 45f);

        for (int index = 0; index < 8; index++)
        {
            float angle = index * 45f;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            CreateIconShape(parent, $"ImpactRay{index}", direction * 48f, new Vector2(22f, 7f), color, angle);
        }
    }

    private void CreateKineticDrivesIcon(Transform parent, Color color)
    {
        CreateChevron(parent, new Vector2(-22f, 0f), color);
        CreateChevron(parent, new Vector2(25f, 0f), color);
    }

    private void CreateChevron(Transform parent, Vector2 position, Color color)
    {
        CreateIconShape(parent, "ChevronUpper", position + new Vector2(-6f, 17f), new Vector2(52f, 10f), color, -42f);
        CreateIconShape(parent, "ChevronLower", position + new Vector2(-6f, -17f), new Vector2(52f, 10f), color, 42f);
    }

    private void CreateThermalVentIcon(Transform parent, Color color)
    {
        CreateIconShape(parent, "VentCore", Vector2.zero, new Vector2(24f, 24f), color, 45f);

        for (int index = 0; index < 4; index++)
        {
            float angle = index * 90f;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            CreateIconShape(parent, $"VentBlade{index}", direction * 33f, new Vector2(42f, 11f), color, angle);
        }
    }

    private RectTransform CreateIconShape(Transform parent, string name, Vector2 position, Vector2 size, Color color, float rotation)
    {
        RectTransform shape = CreateCardElement(parent, name, position, size);
        shape.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image shapeImage = shape.gameObject.AddComponent<Image>();
        shapeImage.color = color;
        shapeImage.raycastTarget = false;
        return shape;
    }

    private RectTransform CreateCardElement(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        element.transform.SetParent(parent, false);

        RectTransform rect = element.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Color GetPowerUpIconColor(CommonPowerUp powerUp)
    {
        switch (powerUp)
        {
            case CommonPowerUp.RapidFire: return new Color(0.35f, 0.92f, 1f);
            case CommonPowerUp.PowerRounds: return new Color(1f, 0.68f, 0.35f);
            case CommonPowerUp.KineticDrives: return new Color(0.74f, 1f, 0.42f);
            case CommonPowerUp.ThermalVent: return new Color(0.7f, 0.8f, 1f);
            default: return commonCardAccentColor;
        }
    }

    private void CreatePowerUpLabel(TMP_Text textSource, Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size)
    {
        TMP_Text label = Instantiate(textSource, parent, false);
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = color;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = position;
        labelRect.sizeDelta = size;
    }

    private void SelectCommonPowerUp(CommonPowerUp powerUp)
    {
        if (!powerUpCardsVisible || waveState != WaveState.PowerUpSelection)
            return;

        ApplyCommonPowerUp(powerUp);

        if (GameFeedback.Instance != null)
            GameFeedback.Instance.PlayPowerUpSelected();

        HideCommonPowerUpCards();
        BeginPowerUpCameraZoomOut();
        waveState = WaveState.PowerUpOutro;
    }

    private void ApplyCommonPowerUp(CommonPowerUp powerUp)
    {
        GameManager gameManager = GameManager.Instance;
        PlayerMovement2D player = gameManager != null ? gameManager.player : null;
        PlayerShooter shooter = player != null ? player.GetComponent<PlayerShooter>() : null;

        switch (powerUp)
        {
            case CommonPowerUp.RapidFire:
                if (shooter != null)
                    shooter.MultiplyFireRate(rapidFireMultiplier);
                break;

            case CommonPowerUp.PowerRounds:
                if (shooter != null)
                    shooter.MultiplyDamage(powerRoundsMultiplier);
                break;

            case CommonPowerUp.KineticDrives:
                if (player != null)
                    player.MultiplyMoveSpeed(kineticDrivesMultiplier);
                break;

            case CommonPowerUp.ThermalVent:
                if (gameManager != null)
                    gameManager.MultiplyDrainHeatBuildRate(thermalVentMultiplier);
                break;
        }
    }

    private void ResetRunPowerUps()
    {
        GameManager gameManager = GameManager.Instance;
        PlayerMovement2D player = gameManager != null ? gameManager.player : null;
        PlayerShooter shooter = player != null ? player.GetComponent<PlayerShooter>() : null;

        if (player != null)
            player.ResetPowerUpModifiers();

        if (shooter != null)
            shooter.ResetPowerUpModifiers();

        if (gameManager != null)
            gameManager.ResetPowerUpModifiers();
    }

    private string GetPowerUpTitle(CommonPowerUp powerUp)
    {
        switch (powerUp)
        {
            case CommonPowerUp.RapidFire: return "RAPID FIRE";
            case CommonPowerUp.PowerRounds: return "POWER ROUNDS";
            case CommonPowerUp.KineticDrives: return "KINETIC DRIVES";
            case CommonPowerUp.ThermalVent: return "THERMAL VENT";
            default: return string.Empty;
        }
    }

    private string GetPowerUpDescription(CommonPowerUp powerUp)
    {
        switch (powerUp)
        {
            case CommonPowerUp.RapidFire:
                return $"{Mathf.RoundToInt((rapidFireMultiplier - 1f) * 100f)}% FASTER SHOOTING";
            case CommonPowerUp.PowerRounds:
                return $"{Mathf.RoundToInt((powerRoundsMultiplier - 1f) * 100f)}% MORE BULLET DAMAGE";
            case CommonPowerUp.KineticDrives:
                return $"{Mathf.RoundToInt((kineticDrivesMultiplier - 1f) * 100f)}% MOVE SPEED";
            case CommonPowerUp.ThermalVent:
                return $"{Mathf.RoundToInt((1f - thermalVentMultiplier) * 100f)}% LESS DRAIN HEAT";
            default:
                return string.Empty;
        }
    }

    private void HideCommonPowerUpCards()
    {
        if (powerUpCardPanel != null)
            Destroy(powerUpCardPanel);

        powerUpCardPanel = null;
        powerUpCardsVisible = false;
    }

    private T GetEnemyPrefab<T>() where T : EnemyBase
    {
        if (enemyOptions == null)
            return null;

        foreach (EnemyOption option in enemyOptions)
        {
            if (option != null && option.prefab is T typedPrefab)
                return typedPrefab;
        }

        return null;
    }

    private void EnsureWaveStatusUI()
    {
        if (waveStatusText != null || GameManager.Instance == null || GameManager.Instance.scoreText == null)
            return;

        TMP_Text source = GameManager.Instance.scoreText;
        // Parent to the full-screen root canvas (like the drain gauge), not the fixed
        // 1920x1080 HUD box, so the status sits just under the gauge on every screen
        // shape instead of overlapping it on phones wider than 16:9.
        Canvas sourceCanvas = source.canvas;
        Transform statusParent = sourceCanvas != null ? sourceCanvas.rootCanvas.transform : source.transform.parent;
        waveStatusText = Instantiate(source, statusParent, false);
        waveStatusText.gameObject.name = "WaveStatusText";
        waveStatusText.alignment = TextAlignmentOptions.Center;
        waveStatusText.color = new Color(0.8f, 0.95f, 1f);
        waveStatusText.margin = Vector4.zero;
        waveStatusText.textWrappingMode = TextWrappingModes.NoWrap;
        waveStatusText.overflowMode = TextOverflowModes.Overflow;
        waveStatusText.raycastTarget = false;

        Canvas statusCanvas = waveStatusText.GetComponent<Canvas>();
        if (statusCanvas == null)
            statusCanvas = waveStatusText.gameObject.AddComponent<Canvas>();

        statusCanvas.overrideSorting = true;
        statusCanvas.sortingOrder = waveStatusSortingOrder;
        waveStatusText.transform.SetAsLastSibling();

        ApplyWaveStatusLayout();
    }

    private void ApplyWaveStatusLayout()
    {
        if (waveStatusText == null)
            return;

        RectTransform rect = waveStatusText.rectTransform;
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -145f);
        rect.sizeDelta = new Vector2(900f, 64f);
        waveStatusText.fontSize = waveStatusFontSize;
    }

    private void UpdateWaveIntroUI()
    {
        RectTransform rect = waveStatusText.rectTransform;
        float elapsed = waveTitleCenterHoldDuration + waveTitleMoveDuration - waveIntroTimer;
        float moveProgress = Mathf.Clamp01((elapsed - waveTitleCenterHoldDuration) / waveTitleMoveDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, moveProgress);

        Vector2 centerAnchor = new Vector2(0.5f, 0.5f);
        Vector2 statusAnchor = new Vector2(0.5f, 1f);
        rect.anchorMin = Vector2.Lerp(centerAnchor, statusAnchor, easedProgress);
        rect.anchorMax = Vector2.Lerp(centerAnchor, statusAnchor, easedProgress);
        rect.pivot = Vector2.Lerp(centerAnchor, statusAnchor, easedProgress);
        rect.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0f, -145f), easedProgress);
        rect.sizeDelta = Vector2.Lerp(new Vector2(1000f, 160f), new Vector2(900f, 64f), easedProgress);
        waveStatusText.fontSize = Mathf.Lerp(waveTitleFontSize, waveStatusFontSize, easedProgress);
        waveStatusText.text = $"WAVE {waveNumber}";
    }

    private void BeginFirstWaveCameraIntro()
    {
        GameManager gameManager = GameManager.Instance;
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null || !gameplayCamera.orthographic || gameManager == null || gameManager.player == null)
            return;

        firstWaveCamera = gameplayCamera;
        firstWaveCameraBasePosition = gameplayCamera.transform.position;
        firstWaveCameraBaseSize = gameplayCamera.orthographicSize;
        Vector3 playerPosition = gameManager.player.transform.position;
        firstWaveCameraFocusedPosition = new Vector3(playerPosition.x, playerPosition.y, firstWaveCameraBasePosition.z);
        firstWaveCameraIntroActive = true;
        firstWaveCameraState = FirstWaveCameraState.ZoomingIn;
        firstWaveCameraAnimationTimer = 0f;
    }

    private void UpdateFirstWaveCameraIntro()
    {
        if (!firstWaveCameraIntroActive || firstWaveCamera == null)
            return;

        if (firstWaveCameraState == FirstWaveCameraState.Holding || firstWaveCameraState == FirstWaveCameraState.None)
            return;

        firstWaveCameraAnimationTimer += Time.deltaTime;
        float duration = firstWaveCameraState == FirstWaveCameraState.ZoomingIn
            ? firstWaveZoomInDuration
            : waveTitleMoveDuration;
        float progress = Mathf.Clamp01(firstWaveCameraAnimationTimer / duration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        bool zoomingIn = firstWaveCameraState == FirstWaveCameraState.ZoomingIn;
        firstWaveCamera.transform.position = zoomingIn
            ? Vector3.Lerp(firstWaveCameraBasePosition, firstWaveCameraFocusedPosition, easedProgress)
            : Vector3.Lerp(firstWaveCameraFocusedPosition, firstWaveCameraBasePosition, easedProgress);
        firstWaveCamera.orthographicSize = zoomingIn
            ? Mathf.Lerp(firstWaveCameraBaseSize, firstWaveCameraBaseSize * firstWaveZoomMultiplier, easedProgress)
            : Mathf.Lerp(firstWaveCameraBaseSize * firstWaveZoomMultiplier, firstWaveCameraBaseSize, easedProgress);

        if (progress >= 1f)
        {
            if (zoomingIn)
            {
                firstWaveCameraState = FirstWaveCameraState.Holding;
                firstWaveCameraAnimationTimer = 0f;
            }
            else
            {
                CompleteFirstWaveCameraIntro();
            }
        }
    }

    private void BeginFirstWaveCameraZoomOut()
    {
        if (!firstWaveCameraIntroActive || firstWaveCameraState != FirstWaveCameraState.Holding)
            return;

        firstWaveCameraState = FirstWaveCameraState.ZoomingOut;
        firstWaveCameraAnimationTimer = 0f;
    }

    private void CompleteFirstWaveCameraIntro()
    {
        if (!firstWaveCameraIntroActive)
            return;

        if (firstWaveCamera != null)
        {
            firstWaveCamera.transform.position = firstWaveCameraBasePosition;
            firstWaveCamera.orthographicSize = firstWaveCameraBaseSize;
        }

        firstWaveCameraIntroActive = false;
        firstWaveCameraState = FirstWaveCameraState.None;
        firstWaveCameraAnimationTimer = 0f;
        firstWaveCamera = null;
    }

    private void ResetFirstWaveCameraIntro()
    {
        CompleteFirstWaveCameraIntro();
    }

    private void UpdateWaveStatusUI()
    {
        if (waveStatusText == null)
            return;

        if (waveState == WaveState.Intermission)
        {
            ApplyWaveStatusLayout();
            waveStatusText.text = string.Empty;
            return;
        }

        if (waveState == WaveState.PowerUpSelection || waveState == WaveState.PowerUpOutro)
        {
            waveStatusText.text = string.Empty;
            return;
        }

        if (waveState == WaveState.WaveIntro)
        {
            UpdateWaveIntroUI();
            return;
        }

        if (waveState == WaveState.Spawning)
        {
            ApplyWaveStatusLayout();
            int enemiesLeft = spawnQueue.Count + activeWaveEnemies.Count;
            waveStatusText.text = $"WAVE {waveNumber}\n{enemiesLeft} LEFT";
        }
    }
}
