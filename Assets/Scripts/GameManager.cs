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

    [Header("Runtime (debug)")]
    public float drainGauge;
    public bool overheated;
    public float currentEnergy;
    public float currentScore;
    public float bestScore;

    private float overheatTimer;
    private bool isGameOver;

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
        // DontDestroyOnLoad(this.gameObject); // enable if you use multiple scenes
    }

    private void Start()
    {
        bestScore = PlayerPrefs.GetFloat("BestScore", 0f);
        EnsureGameOverResultsUI();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateScoreUI();
        UpdateEnergyUI();
        UpdateGaugeUI();
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
            label.enableWordWrapping = false;
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

        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            PlayerPrefs.SetFloat("BestScore", bestScore);
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverScoreText != null) gameOverScoreText.text = "Score: " + Mathf.RoundToInt(currentScore);
        if (bestScoreText != null) bestScoreText.text = "Best: " + Mathf.RoundToInt(bestScore);

        Debug.Log("Game Over");

        spriteRenderer.enabled = false;
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
        if (drainGaugeFill != null)
            drainGaugeFill.fillAmount = drainGaugeMax <= 0f ? 0f : drainGauge / drainGaugeMax;
    }

    public void ResetGame()
    {
        isGameOver = false;
        drainGauge = 0f;
        overheated = false;
        currentEnergy = 0f;
        currentScore = 0f;
        spriteRenderer.enabled = true;
        trailRenderer.enabled = true;
        deathParticles.Stop();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateEnergyUI();
        UpdateScoreUI();
        UpdateGaugeUI();

        DestroyAllEnemies();
    }

    public void RevivePlayer()
    {
        isGameOver = false;

        spriteRenderer.enabled = true;
        trailRenderer.enabled = true;
        deathParticles.Stop();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        DestroyAllEnemies();

        // Optionally restore some energy or score on revive
        // currentEnergy = Mathf.Max(currentEnergy, 10f);
        // UpdateEnergyUI();
    }

    public void DestroyAllEnemies()
    {
        EnemyBase[] enemies = FindObjectsOfType<EnemyBase>();
        foreach (EnemyBase enemy in enemies)
        {
            Destroy(enemy.gameObject);
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
    
}
