using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyOption
    {
        public EnemyBase prefab;

        [Header("Spawn Rate (per type)")]
        public float startInterval = 2f;
        public float minInterval = 0.6f;
        public float intervalDecreasePerMinute = 0.5f;

        [Header("Spawn Weight (chance each tick)")]
        [Range(0f, 1f)] public float spawnChance = 1f; // 1 = always spawn when timer fires

        [Header("Optional Limits")]
        public int maxAlive = 999;

        [HideInInspector] public float currentInterval;
        [HideInInspector] public float timer;
    }

    [Header("Enemy Types")]
    public EnemyOption[] enemyOptions;

    [Header("Spawn Settings")]
    public float spawnDistanceFromCenter = 8f;

    private bool gameStarted = false;

    private void Start()
    {
        // Initialize per-type timers
        for (int i = 0; i < enemyOptions.Length; i++)
        {
            enemyOptions[i].currentInterval = enemyOptions[i].startInterval;
            enemyOptions[i].timer = enemyOptions[i].currentInterval;
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (!gameStarted) return;

        Vector3 center = Vector3.zero;
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            center = GameManager.Instance.player.transform.position;

        for (int i = 0; i < enemyOptions.Length; i++)
        {
            EnemyOption opt = enemyOptions[i];
            if (opt.prefab == null) continue;

            // tick timer
            opt.timer -= Time.deltaTime;

            if (opt.timer <= 0f)
            {
                // respect max alive cap
                bool underCap = CountAliveOfType(opt.prefab.GetType()) < opt.maxAlive;

                // spawnChance acts like weight
                if (underCap && Random.value <= opt.spawnChance)
                {
                    Spawn(opt.prefab, center);
                }

                opt.timer = opt.currentInterval;
            }

            // per-type difficulty ramp
            float decreasePerSecond = opt.intervalDecreasePerMinute / 60f;
            opt.currentInterval -= decreasePerSecond * Time.deltaTime;
            opt.currentInterval = Mathf.Max(opt.minInterval, opt.currentInterval);

            enemyOptions[i] = opt; // keep changes
        }
    }

    private void Spawn(EnemyBase prefab, Vector3 center)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnDistanceFromCenter;
        Vector3 spawnPos = center + offset;

        Instantiate(prefab, spawnPos, Quaternion.identity);
    }

    private int CountAliveOfType(System.Type type)
    {
        EnemyBase[] all = FindObjectsOfType<EnemyBase>();
        int count = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetType() == type)
                count++;
        }
        return count;
    }

    public void StartSpawning() => gameStarted = true;
    public void StopSpawning() => gameStarted = false;
}
