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

    private enum PowerUp
    {
        // Common: stat boosts, stack with repeat picks.
        RapidFire,
        PowerRounds,
        KineticDrives,
        ThermalVent,

        // Rare: change how you play, once per run.
        TwinShot,
        PiercingShot,
        OverheatNova
    }

    private static readonly PowerUp[] CommonPowerUps =
        { PowerUp.RapidFire, PowerUp.PowerRounds, PowerUp.KineticDrives, PowerUp.ThermalVent };

    private static readonly PowerUp[] RarePowerUps =
        { PowerUp.TwinShot, PowerUp.PiercingShot, PowerUp.OverheatNova };

    [Header("Enemy Types")]
    public EnemyOption[] enemyOptions;

    [Header("Wave Timing")]
    [SerializeField, Min(0f)] private float firstWaveDelay = 3f;
    [SerializeField, Min(0.05f)] private float enemySpawnInterval = 0.45f;
    // No between-wave intermission setting: the power-up card selection is the
    // breather, and the next wave's title intro plays before spawning starts.
    [Tooltip("Breather before the interrupted wave replays after a revive.")]
    [SerializeField, Min(0f)] private float reviveRecoveryDelay = 2f;

    [Header("Wave Spawn Position")]
    [Tooltip("Spawn enemies just outside the visible screen (inside the arena), so they enter from the edges and the off-screen arrows announce them. Off: spawn at the distances below around the player.")]
    [SerializeField] private bool spawnOffScreen = true;
    [Tooltip("How far past the screen edge enemies appear, in world units.")]
    [SerializeField, Min(0f)] private float offScreenSpawnMargin = 2.5f;

    [Header("Wave Spawn Distance (when not off screen, or no room off screen)")]
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
    [Tooltip("How long the chosen card stays lit (others dimmed) before the run resumes.")]
    [SerializeField, Min(0f)] private float powerUpChoiceHoldDuration = 0.32f;
    // 0.92 alpha in linear colour space looks like roughly a 70% dim.
    [SerializeField] private Color powerUpBackdropColor = new Color(0f, 0f, 0f, 0.92f);
    // Buffs are > 1 (stacking multiplicatively per pick); Thermal Vent scales heat build-up, so it's < 1.
    // Power Rounds breakpoints at base damage 20: 1 pick -> shooters (45 HP) die in 2 shots,
    // 2 picks -> dashers (30 HP) die in 1. Chasers (20 HP) already die in 1.
    [SerializeField, Range(1f, 2f)] private float rapidFireMultiplier = 1.25f;
    [SerializeField, Range(1f, 2f)] private float powerRoundsMultiplier = 1.25f;
    [SerializeField, Range(1f, 2f)] private float kineticDrivesMultiplier = 1.15f;
    [SerializeField, Range(0.5f, 1f)] private float thermalVentMultiplier = 0.8f;

    [Header("Rare Power-Up Cards")]
    [Tooltip("First cleared wave after which a rare card can appear.")]
    [SerializeField, Min(1)] private int rareMinimumWave = 2;
    [Tooltip("Chance that one of the three cards is a rare.")]
    [SerializeField, Range(0f, 1f)] private float rareOfferChance = 0.35f;
    [Tooltip("Guarantee a rare after this many card picks without one.")]
    [SerializeField, Min(1)] private int rarePityAfterPicks = 3;
    [Tooltip("Piercing Shot: extra enemies each bullet passes through.")]
    [SerializeField, Range(1, 5)] private int piercingExtraHits = 2;

    private readonly HashSet<PowerUp> ownedRarePowerUps = new HashSet<PowerUp>();
    private readonly Dictionary<PowerUp, int> commonPickCounts = new Dictionary<PowerUp, int>();
    private int picksWithoutRare;
    private readonly List<NeonPowerUpCard> powerUpCardViews = new List<NeonPowerUpCard>();
    private bool powerUpChoiceLocked;
    private Coroutine powerUpChoiceRoutine;

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
        {
            powerUpCardPanel.SetActive(!isGameOver);
            if (gameManager != null)
                gameManager.SetDrainGaugeVisible(isGameOver);
        }

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

    // A random point at the wave's spawn distance around the player, inside the
    // arena walls. Near a wall, directions that land outside are re-rolled
    // (clamping instead could drop an enemy right next to the player).
    private Vector3 GetSpawnPosition(Vector3 center)
    {
        if (TryGetOffScreenSpawnPosition(center.z, out Vector3 offScreen))
            return offScreen;

        const float wallMargin = 1f;
        float distance = CurrentSpawnDistance();
        Arena arena = Arena.Instance;
        Vector3 candidate = center;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            candidate = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
            if (arena == null || arena.Contains(candidate, wallMargin))
                return candidate;
        }

        Vector2 clamped = arena.Clamp(candidate, wallMargin);
        return new Vector3(clamped.x, clamped.y, candidate.z);
    }

    // A random point offScreenSpawnMargin past the edge of the current view, in
    // a random direction from the screen centre, that is still inside the arena
    // walls. When the camera is against a wall, that side has no room, so
    // another direction is tried. False if there's no room anywhere.
    private bool TryGetOffScreenSpawnPosition(float z, out Vector3 position)
    {
        position = Vector3.zero;
        const float wallMargin = 1f;
        Camera gameplayCamera = Camera.main;
        Arena arena = Arena.Instance;
        if (!spawnOffScreen || arena == null || gameplayCamera == null || !gameplayCamera.orthographic)
            return false;

        Vector2 cameraCenter = gameplayCamera.transform.position;
        Vector2 halfView = new Vector2(gameplayCamera.orthographicSize * gameplayCamera.aspect, gameplayCamera.orthographicSize)
            + Vector2.one * offScreenSpawnMargin;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float toEdge = Mathf.Min(
                halfView.x / Mathf.Max(Mathf.Abs(direction.x), 0.0001f),
                halfView.y / Mathf.Max(Mathf.Abs(direction.y), 0.0001f));
            Vector2 point = cameraCenter + direction * toEdge;

            if (arena.Contains(point, wallMargin))
            {
                position = new Vector3(point.x, point.y, z);
                return true;
            }
        }

        return false;
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
        powerUpCameraFocusedPosition = ClampCameraToArena(
            new Vector3(playerPosition.x, playerPosition.y, powerUpCameraBasePosition.z),
            powerUpCameraBaseSize * powerUpZoomMultiplier);
        powerUpCameraState = PowerUpCameraState.ZoomingIn;
        powerUpCameraAnimationTimer = 0f;
        SetCameraFollowHold("PowerUpZoom", true);
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
            : Vector3.Lerp(powerUpCameraFocusedPosition, SettledCameraPosition(powerUpCameraBasePosition, powerUpCameraBaseSize), easedProgress);
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
            powerUpCamera.transform.position = SettledCameraPosition(powerUpCameraBasePosition, powerUpCameraBaseSize);
            powerUpCamera.orthographicSize = powerUpCameraBaseSize;
        }

        powerUpCameraState = PowerUpCameraState.None;
        powerUpCameraAnimationTimer = 0f;
        powerUpCamera = null;
        SetCameraFollowHold("PowerUpZoom", false);
    }

    // ---------- Follow camera helpers ----------

    // Where the camera rests after a zoom: centred on the player and inside the
    // arena, or the old fixed spot when there's no follow camera.
    private static Vector3 SettledCameraPosition(Vector3 fallback, float orthoSize)
    {
        CameraFollow follow = CameraFollow.Instance;
        GameManager gameManager = GameManager.Instance;
        if (follow == null || gameManager == null || gameManager.player == null)
            return fallback;

        return follow.SettledPositionFor(gameManager.player.transform.position, orthoSize);
    }

    private static Vector3 ClampCameraToArena(Vector3 position, float orthoSize)
    {
        return CameraFollow.Instance != null ? CameraFollow.Instance.ClampToArena(position, orthoSize) : position;
    }

    private static void SetCameraFollowHold(string reason, bool held)
    {
        if (CameraFollow.Instance != null)
            CameraFollow.Instance.SetHold(reason, held);
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

        // Dark enough that the glowing cards pop and the HUD underneath recedes.
        Image panelBackground = powerUpCardPanel.GetComponent<Image>();
        panelBackground.color = powerUpBackdropColor;
        panelBackground.raycastTarget = false;

        // Pink like the GAME OVER title, over a white heading.
        TMP_Text cleared = CreatePowerUpLabel(textSource, powerUpCardPanel.transform, $"WAVE {waveNumber} CLEARED", 22f, ShootingCardColor,
            new Vector2(0f, 395f), new Vector2(1200f, 40f));
        cleared.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, new Color(1f, 0.3f, 0.46f, 0.5f), 0.1f, 0.28f);
        cleared.characterSpacing = 12f;

        TMP_Text title = CreatePowerUpLabel(textSource, powerUpCardPanel.transform, "CHOOSE A POWER-UP", 40f, new Color(0.95f, 0.95f, 0.95f, 1f),
            new Vector2(0f, 348f), new Vector2(1200f, 64f));
        title.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, new Color(1f, 1f, 1f, 0.22f), 0.1f, 0.28f);
        title.characterSpacing = 10f;

        powerUpCardViews.Clear();
        powerUpChoiceLocked = false;

        List<PowerUp> choices = BuildCommonPowerUpChoices();
        for (int index = 0; index < choices.Count; index++)
        {
            PowerUp choice = choices[index];
            int cardIndex = index;
            float raise = IsRare(choice) ? 14f : 0f;
            NeonPowerUpCard card = NeonPowerUpCard.Create(powerUpCardPanel.transform, textSource, BuildCardSpec(choice),
                new Vector2((index - 1) * 420f, -10f + raise), index * 0.07f);
            card.Button.onClick.AddListener(() => OnPowerUpCardTapped(cardIndex, choice));
            powerUpCardViews.Add(card);
        }

        // The heat gauge would sit right behind the header; it's back when the cards close.
        gameManager.SetDrainGaugeVisible(false);

        powerUpCardsVisible = true;
        return true;
    }

    // Light up the chosen card, dim the others, then apply it after a beat.
    private void OnPowerUpCardTapped(int cardIndex, PowerUp powerUp)
    {
        if (powerUpChoiceLocked || !powerUpCardsVisible || waveState != WaveState.PowerUpSelection)
            return;

        powerUpChoiceLocked = true;
        for (int index = 0; index < powerUpCardViews.Count; index++)
        {
            if (powerUpCardViews[index] == null)
                continue;

            if (index == cardIndex)
                powerUpCardViews[index].SetChosen();
            else
                powerUpCardViews[index].SetDimmed();
        }

        if (GameFeedback.Instance != null)
            GameFeedback.Instance.PlayPowerUpSelected();

        powerUpChoiceRoutine = StartCoroutine(FinishPowerUpChoice(powerUp));
    }

    private System.Collections.IEnumerator FinishPowerUpChoice(PowerUp powerUp)
    {
        yield return new WaitForSecondsRealtime(powerUpChoiceHoldDuration);
        powerUpChoiceRoutine = null;

        // Died during the beat (stray enemy shot): drop these cards; a fresh set
        // is offered once the player is back.
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            HideCommonPowerUpCards();
            yield break;
        }

        SelectCommonPowerUp(powerUp);
    }

    private NeonCardSpec BuildCardSpec(PowerUp powerUp)
    {
        bool rare = IsRare(powerUp);
        int level = commonPickCounts.TryGetValue(powerUp, out int picks) ? picks : 0;

        return new NeonCardSpec
        {
            title = GetPowerUpTitle(powerUp),
            rare = rare,
            accent = GetPowerUpColor(powerUp),
            stat = rare ? null : GetPowerUpStat(powerUp),
            statLabel = rare ? null : GetPowerUpStatLabel(powerUp),
            description = rare ? GetPowerUpDescription(powerUp) : null,
            footer = rare ? "NEW ABILITY" : $"<alpha=#80>LV {level}  >  <alpha=#FF>LV {level + 1}",
            drawGlyph = (root, color) => DrawPowerUpGlyph(root, powerUp, color)
        };
    }

    // Three different commons, and sometimes one of them swapped for a rare the
    // player doesn't own yet (from rareMinimumWave on, with a pity guarantee).
    private List<PowerUp> BuildCommonPowerUpChoices()
    {
        List<PowerUp> choices = new List<PowerUp>(CommonPowerUps);
        Shuffle(choices);
        choices.RemoveRange(3, choices.Count - 3);

        List<PowerUp> availableRares = new List<PowerUp>();
        foreach (PowerUp rare in RarePowerUps)
        {
            if (!ownedRarePowerUps.Contains(rare))
                availableRares.Add(rare);
        }

        bool offerRare = availableRares.Count > 0 &&
            waveNumber >= rareMinimumWave &&
            (picksWithoutRare >= rarePityAfterPicks || Random.value < rareOfferChance);

        if (offerRare)
        {
            choices[Random.Range(0, choices.Count)] = availableRares[Random.Range(0, availableRares.Count)];
            picksWithoutRare = 0;
        }
        else if (waveNumber >= rareMinimumWave && availableRares.Count > 0)
        {
            picksWithoutRare++;
        }

        return choices;
    }

    private static void Shuffle(List<PowerUp> list)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int randomIndex = Random.Range(0, index + 1);
            PowerUp temporary = list[index];
            list[index] = list[randomIndex];
            list[randomIndex] = temporary;
        }
    }

    private static bool IsRare(PowerUp powerUp)
    {
        return System.Array.IndexOf(RarePowerUps, powerUp) >= 0;
    }

    // The ability symbol, drawn in a 142 x 142 space (the card scales it onto its orb).
    private void DrawPowerUpGlyph(RectTransform root, PowerUp powerUp, Color color)
    {
        switch (powerUp)
        {
            case PowerUp.RapidFire: CreateRapidFireIcon(root, color); break;
            case PowerUp.PowerRounds: CreatePowerRoundsIcon(root, color); break;
            case PowerUp.KineticDrives: CreateKineticDrivesIcon(root, color); break;
            case PowerUp.ThermalVent: CreateThermalVentIcon(root, color); break;
            case PowerUp.TwinShot: CreateTwinShotIcon(root, color); break;
            case PowerUp.PiercingShot: CreatePiercingShotIcon(root, color); break;
            case PowerUp.OverheatNova: CreateOverheatNovaIcon(root, color); break;
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

    // Two bolts splitting apart from one origin.
    private void CreateTwinShotIcon(Transform parent, Color color)
    {
        CreateIconShape(parent, "Origin", new Vector2(-44f, 0f), new Vector2(20f, 20f), color, 45f);
        CreateIconShape(parent, "BoltUpperTrail", new Vector2(-4f, 17f), new Vector2(74f, 9f), color, 24f);
        CreateIconShape(parent, "BoltUpperHead", new Vector2(36f, 35f), new Vector2(22f, 22f), color, 45f);
        CreateIconShape(parent, "BoltLowerTrail", new Vector2(-4f, -17f), new Vector2(74f, 9f), color, -24f);
        CreateIconShape(parent, "BoltLowerHead", new Vector2(36f, -35f), new Vector2(22f, 22f), color, 45f);
    }

    // A lance running straight through two targets.
    private void CreatePiercingShotIcon(Transform parent, Color color)
    {
        Color target = new Color(color.r, color.g, color.b, 0.45f);
        CreateIconShape(parent, "TargetNear", new Vector2(-14f, 0f), new Vector2(30f, 30f), target, 45f);
        CreateIconShape(parent, "TargetFar", new Vector2(28f, 0f), new Vector2(30f, 30f), target, 45f);
        CreateIconShape(parent, "Lance", new Vector2(-2f, 0f), new Vector2(112f, 8f), color, 0f);
        CreateIconShape(parent, "LanceHead", new Vector2(54f, 0f), new Vector2(20f, 20f), color, 45f);
    }

    // A core with a dashed shockwave ring.
    private void CreateOverheatNovaIcon(Transform parent, Color color)
    {
        CreateIconShape(parent, "NovaCore", Vector2.zero, new Vector2(30f, 30f), color, 45f);

        const int segments = 12;
        for (int index = 0; index < segments; index++)
        {
            float angle = index * 360f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            CreateIconShape(parent, $"Shockwave{index}", direction * 52f, new Vector2(20f, 7f), color, angle + 90f);
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

    // Card colour from the game's own palette, by what the power-up affects:
    // pink like the fire button (shooting), cyan like the drain ring (drain and
    // heat), white like the HUD and pills (movement).
    private static readonly Color ShootingCardColor = new Color(1f, 0.3f, 0.46f);
    private static readonly Color DrainCardColor = new Color(0.16f, 0.93f, 0.97f);
    private static readonly Color MovementCardColor = new Color(0.95f, 0.95f, 0.95f);

    private static Color GetPowerUpColor(PowerUp powerUp)
    {
        switch (powerUp)
        {
            case PowerUp.ThermalVent:
            case PowerUp.OverheatNova:
                return DrainCardColor;
            case PowerUp.KineticDrives:
                return MovementCardColor;
            default:
                return ShootingCardColor;
        }
    }

    private string GetPowerUpStat(PowerUp powerUp)
    {
        switch (powerUp)
        {
            case PowerUp.RapidFire: return $"+{Mathf.RoundToInt((rapidFireMultiplier - 1f) * 100f)}%";
            case PowerUp.PowerRounds: return $"+{Mathf.RoundToInt((powerRoundsMultiplier - 1f) * 100f)}%";
            case PowerUp.KineticDrives: return $"+{Mathf.RoundToInt((kineticDrivesMultiplier - 1f) * 100f)}%";
            case PowerUp.ThermalVent: return $"-{Mathf.RoundToInt((1f - thermalVentMultiplier) * 100f)}%";
            default: return string.Empty;
        }
    }

    private static string GetPowerUpStatLabel(PowerUp powerUp)
    {
        switch (powerUp)
        {
            case PowerUp.RapidFire: return "FIRE RATE";
            case PowerUp.PowerRounds: return "BULLET DAMAGE";
            case PowerUp.KineticDrives: return "MOVE SPEED";
            case PowerUp.ThermalVent: return "DRAIN HEAT";
            default: return string.Empty;
        }
    }

    private TMP_Text CreatePowerUpLabel(TMP_Text textSource, Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size)
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
        return label;
    }

    // The tap already played the select sound (OnPowerUpCardTapped).
    private void SelectCommonPowerUp(PowerUp powerUp)
    {
        if (!powerUpCardsVisible || waveState != WaveState.PowerUpSelection)
            return;

        ApplyCommonPowerUp(powerUp);

        if (IsRare(powerUp))
            ownedRarePowerUps.Add(powerUp);
        else
            commonPickCounts[powerUp] = (commonPickCounts.TryGetValue(powerUp, out int picks) ? picks : 0) + 1;

        HideCommonPowerUpCards();
        BeginPowerUpCameraZoomOut();
        waveState = WaveState.PowerUpOutro;
    }

    private void ApplyCommonPowerUp(PowerUp powerUp)
    {
        GameManager gameManager = GameManager.Instance;
        PlayerMovement2D player = gameManager != null ? gameManager.player : null;
        PlayerShooter shooter = player != null ? player.GetComponent<PlayerShooter>() : null;

        switch (powerUp)
        {
            case PowerUp.RapidFire:
                if (shooter != null)
                    shooter.MultiplyFireRate(rapidFireMultiplier);
                break;

            case PowerUp.PowerRounds:
                if (shooter != null)
                    shooter.MultiplyDamage(powerRoundsMultiplier);
                break;

            case PowerUp.KineticDrives:
                if (player != null)
                    player.MultiplyMoveSpeed(kineticDrivesMultiplier);
                break;

            case PowerUp.ThermalVent:
                if (gameManager != null)
                    gameManager.MultiplyDrainHeatBuildRate(thermalVentMultiplier);
                break;

            case PowerUp.TwinShot:
                if (shooter != null)
                    shooter.EnableTwinShot();
                break;

            case PowerUp.PiercingShot:
                if (shooter != null)
                    shooter.AddPierce(piercingExtraHits);
                break;

            case PowerUp.OverheatNova:
                if (gameManager != null)
                    gameManager.EnableOverheatNova();
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

        ownedRarePowerUps.Clear();
        commonPickCounts.Clear();
        picksWithoutRare = 0;
    }

    private string GetPowerUpTitle(PowerUp powerUp)
    {
        switch (powerUp)
        {
            case PowerUp.RapidFire: return "RAPID FIRE";
            case PowerUp.PowerRounds: return "POWER ROUNDS";
            case PowerUp.KineticDrives: return "KINETIC DRIVES";
            case PowerUp.ThermalVent: return "THERMAL VENT";
            case PowerUp.TwinShot: return "TWIN SHOT";
            case PowerUp.PiercingShot: return "PIERCING SHOT";
            case PowerUp.OverheatNova: return "OVERHEAT NOVA";
            default: return string.Empty;
        }
    }

    private string GetPowerUpDescription(PowerUp powerUp)
    {
        switch (powerUp)
        {
            case PowerUp.RapidFire:
                return $"{Mathf.RoundToInt((rapidFireMultiplier - 1f) * 100f)}% FASTER SHOOTING";
            case PowerUp.PowerRounds:
                return $"{Mathf.RoundToInt((powerRoundsMultiplier - 1f) * 100f)}% MORE BULLET DAMAGE";
            case PowerUp.KineticDrives:
                return $"{Mathf.RoundToInt((kineticDrivesMultiplier - 1f) * 100f)}% MOVE SPEED";
            case PowerUp.ThermalVent:
                return $"{Mathf.RoundToInt((1f - thermalVentMultiplier) * 100f)}% LESS DRAIN HEAT";
            case PowerUp.TwinShot:
                return "EVERY SHOT FIRES A SECOND BULLET";
            case PowerUp.PiercingShot:
                return $"BULLETS PIERCE {piercingExtraHits} MORE ENEMIES";
            case PowerUp.OverheatNova:
                return "OVERHEATING BLASTS NEARBY ENEMIES";
            default:
                return string.Empty;
        }
    }

    private void HideCommonPowerUpCards()
    {
        if (powerUpChoiceRoutine != null)
            StopCoroutine(powerUpChoiceRoutine);

        powerUpChoiceRoutine = null;
        powerUpChoiceLocked = false;
        powerUpCardViews.Clear();

        if (powerUpCardPanel != null)
            Destroy(powerUpCardPanel);

        powerUpCardPanel = null;
        powerUpCardsVisible = false;

        if (GameManager.Instance != null)
            GameManager.Instance.SetDrainGaugeVisible(true);
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
        firstWaveCameraFocusedPosition = ClampCameraToArena(
            new Vector3(playerPosition.x, playerPosition.y, firstWaveCameraBasePosition.z),
            firstWaveCameraBaseSize * firstWaveZoomMultiplier);
        firstWaveCameraIntroActive = true;
        firstWaveCameraState = FirstWaveCameraState.ZoomingIn;
        firstWaveCameraAnimationTimer = 0f;
        SetCameraFollowHold("FirstWaveIntro", true);
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
            : Vector3.Lerp(firstWaveCameraFocusedPosition, SettledCameraPosition(firstWaveCameraBasePosition, firstWaveCameraBaseSize), easedProgress);
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
            firstWaveCamera.transform.position = SettledCameraPosition(firstWaveCameraBasePosition, firstWaveCameraBaseSize);
            firstWaveCamera.orthographicSize = firstWaveCameraBaseSize;
        }

        firstWaveCameraIntroActive = false;
        firstWaveCameraState = FirstWaveCameraState.None;
        firstWaveCameraAnimationTimer = 0f;
        firstWaveCamera = null;
        SetCameraFollowHold("FirstWaveIntro", false);
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
