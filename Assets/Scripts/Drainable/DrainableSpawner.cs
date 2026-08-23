using UnityEngine;

public class DrainableSpawner : MonoBehaviour
{
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

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        for (int i = 0; i < spawnOptions.Length; i++)
        {
            spawnOptions[i].currentInterval = spawnOptions[i].startInterval;
            spawnOptions[i].timer = spawnOptions[i].currentInterval;
        }

        spawning = startOnPlay;
    }

    private void Update()
    {
        if (!spawning) return;

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
        if (cam == null) cam = Camera.main;

        // For 2D orthographic, z doesn't matter much, but we keep it stable.
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, cam.nearClipPlane));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, cam.nearClipPlane));

        float x = Random.Range(bl.x + margin, tr.x - margin);
        float y = Random.Range(bl.y + margin, tr.y - margin);

        return new Vector3(x, y, spawnZ);
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

    public void StartSpawning() => spawning = true;
    public void StopSpawning() => spawning = false;
}
