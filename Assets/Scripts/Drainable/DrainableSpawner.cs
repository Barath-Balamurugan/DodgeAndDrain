using UnityEngine;

public class DrainableSpawner : MonoBehaviour
{
    public static DrainableSpawner Instance { get; private set; }
    public static bool AreDrainablesPaused { get; private set; }

    [System.Serializable]
    public class SpawnOption
    {
        public GameObject prefab;

        [Header("Spawn Rate (per type)")]
        public float startInterval = 2f;
        public float minInterval = 0.6f;
        public float intervalDecreasePerMinute = 0.5f;

        [Header("Spawn Weight (chance each tick)")]
        [Range(0f, 1f)] public float spawnChance = 1f;

        [Header("Optional Limits")]
        public int maxAlive = 999;

        [HideInInspector] public float currentInterval;
        [HideInInspector] public float timer;
    }

    [Header("Spawn Types")]
    public SpawnOption[] spawnOptions;

    [Header("Camera Bounds")]
    public Camera cam;
    public float margin = 0.5f;          // keep away from edges (world units)
    public float spawnZ = 0f;            // 2D usually 0

    [Header("State")]
    public bool startOnPlay = true;

    private bool spawning;
    private Vector3 arenaMin;
    private Vector3 arenaMax;
    private bool hasCachedArenaBounds;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        CacheArenaBounds();
        ResetSpawnTimers();

        spawning = startOnPlay;
    }

    private void Update()
    {
        if (!spawning || AreDrainablesPaused) return;

        for (int i = 0; i < spawnOptions.Length; i++)
        {
            SpawnOption opt = spawnOptions[i];
            if (opt.prefab == null) continue;

            opt.timer -= Time.deltaTime;

            if (opt.timer <= 0f)
            {
                bool underCap = CountAliveOfPrefab(opt.prefab) < opt.maxAlive;

                if (underCap && Random.value <= opt.spawnChance)
                {
                    Vector3 pos = GetRandomPointInCameraBounds();
                    Instantiate(opt.prefab, pos, Quaternion.identity);
                }

                opt.timer = opt.currentInterval;
            }

            float decreasePerSecond = opt.intervalDecreasePerMinute / 60f;
            opt.currentInterval -= decreasePerSecond * Time.deltaTime;
            opt.currentInterval = Mathf.Max(opt.minInterval, opt.currentInterval);

            spawnOptions[i] = opt;
        }
    }

    private Vector3 GetRandomPointInCameraBounds()
    {
        if (!hasCachedArenaBounds)
            CacheArenaBounds();

        float x = Random.Range(arenaMin.x + margin, arenaMax.x - margin);
        float y = Random.Range(arenaMin.y + margin, arenaMax.y - margin);

        return new Vector3(x, y, spawnZ);
    }

    private void CacheArenaBounds()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        float planeDistance = Mathf.Abs(cam.transform.position.z - spawnZ);
        arenaMin = cam.ViewportToWorldPoint(new Vector3(0f, 0f, planeDistance));
        arenaMax = cam.ViewportToWorldPoint(new Vector3(1f, 1f, planeDistance));
        hasCachedArenaBounds = true;
    }

    public bool TryGetArenaBounds(out Vector3 min, out Vector3 max)
    {
        if (!hasCachedArenaBounds)
            CacheArenaBounds();

        min = arenaMin;
        max = arenaMax;
        return hasCachedArenaBounds;
    }

    private int CountAliveOfPrefab(GameObject prefab)
    {
        // Lightweight approach: count by name. For most games this is fine.
        // If you need exact counts even when renaming, add a tag or marker component instead.
        GameObject[] all = GameObject.FindObjectsOfType<GameObject>();
        int count = 0;

        string prefabName = prefab.name;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == prefabName + "(Clone)")
                count++;
        }
        return count;
    }

    public void StartSpawning()
    {
        AreDrainablesPaused = false;
        spawning = true;
    }

    public void StopSpawning()
    {
        AreDrainablesPaused = false;
        spawning = false;
    }

    public void PauseForPowerUpSelection()
    {
        AreDrainablesPaused = true;
    }

    public void ResumeAfterPowerUpSelection()
    {
        AreDrainablesPaused = false;
        spawning = true;
    }

    public void ResetForNewRun()
    {
        AreDrainablesPaused = false;
        ResetSpawnTimers();

        DrainableBase[] drainables = FindObjectsOfType<DrainableBase>();
        foreach (DrainableBase drainable in drainables)
        {
            if (drainable != null)
                Destroy(drainable.gameObject);
        }
    }

    private void ResetSpawnTimers()
    {
        for (int index = 0; index < spawnOptions.Length; index++)
        {
            spawnOptions[index].currentInterval = spawnOptions[index].startInterval;
            spawnOptions[index].timer = spawnOptions[index].currentInterval;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            AreDrainablesPaused = false;
        }
    }
}
