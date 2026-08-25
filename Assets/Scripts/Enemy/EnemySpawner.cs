using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
        Spawning,
        Intermission
    }

    [Header("Enemy Types")]
    public EnemyOption[] enemyOptions;

    [Header("Wave Timing")]
    [SerializeField, Min(0f)] private float firstWaveDelay = 3f;
    [SerializeField, Min(0.05f)] private float enemySpawnInterval = 0.45f;
    [SerializeField, Min(0f)] private float intermissionDuration = 3f;
    [SerializeField, Min(0f)] private float spawnDistanceFromCenter = 8f;

    [Header("Wave Rewards")]
    [SerializeField, Min(0)] private int ammoRewardPerWave = 5;
    [SerializeField, Min(0f)] private float drainHeatReductionPerWave = 30f;

    [Header("Wave UI")]
    [SerializeField] private TMP_Text waveStatusText;

    private readonly List<EnemyBase> spawnQueue = new List<EnemyBase>();
    private readonly HashSet<EnemyBase> activeWaveEnemies = new HashSet<EnemyBase>();

    private WaveState waveState = WaveState.Idle;
    private bool gameStarted;
    private bool resetWhenGameOverEnds;
    private int waveNumber;
    private float nextSpawnTimer;
    private float intermissionTimer;
    private float announcementTimer;

    private void Update()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.IsGameOver)
        {
            resetWhenGameOverEnds = gameStarted;
            return;
        }

        if (resetWhenGameOverEnds)
        {
            ResetWaves();
            resetWhenGameOverEnds = false;
        }

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

            case WaveState.Spawning:
                SpawnWaveEnemies();
                if (spawnQueue.Count == 0 && activeWaveEnemies.Count == 0)
                    CompleteWave();
                break;
        }

        UpdateWaveStatusUI();
    }

    public void StartSpawning()
    {
        gameStarted = true;
        ResetWaves();
    }

    public void StopSpawning()
    {
        gameStarted = false;
        waveState = WaveState.Idle;

        if (waveStatusText != null)
            waveStatusText.text = string.Empty;
    }

    private void ResetWaves()
    {
        spawnQueue.Clear();
        activeWaveEnemies.Clear();
        waveNumber = 0;
        announcementTimer = 0f;
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
        waveState = WaveState.Spawning;
        nextSpawnTimer = 0f;
        announcementTimer = 1.25f;
        BuildWaveQueue(waveNumber);
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
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnDistanceFromCenter;
        return center + offset;
    }

    private void RemoveDefeatedWaveEnemies()
    {
        activeWaveEnemies.RemoveWhere(enemy => enemy == null);
    }

    private void CompleteWave()
    {
        BeginIntermission(intermissionDuration, true);
    }

    private void GiveWaveReward()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        PlayerAmmo ammo = gameManager.player != null ? gameManager.player.GetComponent<PlayerAmmo>() : null;
        if (ammo != null && ammoRewardPerWave > 0)
            ammo.Add(ammoRewardPerWave);

        gameManager.CoolDrainHeat(drainHeatReductionPerWave);
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
        waveStatusText = Instantiate(source, source.transform.parent, false);
        waveStatusText.gameObject.name = "WaveStatusText";
        waveStatusText.alignment = TextAlignmentOptions.Center;
        waveStatusText.fontSize = 24f;
        waveStatusText.color = new Color(0.8f, 0.95f, 1f);
        waveStatusText.margin = Vector4.zero;
        waveStatusText.enableWordWrapping = false;
        waveStatusText.overflowMode = TextOverflowModes.Overflow;
        waveStatusText.raycastTarget = false;

        RectTransform rect = waveStatusText.rectTransform;
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -145f);
        rect.sizeDelta = new Vector2(900f, 64f);
    }

    private void UpdateWaveStatusUI()
    {
        if (waveStatusText == null)
            return;

        if (waveState == WaveState.Intermission)
        {
            if (waveNumber == 0)
            {
                waveStatusText.text = $"WAVE 1 IN {Mathf.CeilToInt(Mathf.Max(intermissionTimer, 0f))}";
            }
            else
            {
                waveStatusText.text = $"WAVE {waveNumber} COMPLETE  •  +{ammoRewardPerWave} AMMO\nNEXT WAVE IN {Mathf.CeilToInt(Mathf.Max(intermissionTimer, 0f))}";
            }

            return;
        }

        if (waveState == WaveState.Spawning)
        {
            announcementTimer -= Time.deltaTime;
            int enemiesLeft = spawnQueue.Count + activeWaveEnemies.Count;
            waveStatusText.text = announcementTimer > 0f
                ? $"WAVE {waveNumber}"
                : $"WAVE {waveNumber}  •  {enemiesLeft} LEFT";
        }
    }
}
