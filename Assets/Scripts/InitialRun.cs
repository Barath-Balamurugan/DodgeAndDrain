using UnityEngine;

public class InitialRun : MonoBehaviour
{
    public GameObject tutorialPanel;
    public GameObject startPanel;
    public EnemySpawner enemySpawner;
    public DrainableSpawner drainableSpawner;
    public GameObject player;

    private const string FirstRunKey = "FirstRunCompleted";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // PlayerPrefs.DeleteAll();
    }

    public void StartTutorial()
    {
        if (!PlayerPrefs.HasKey(FirstRunKey))
        {
            // Show the tutorial panel (first launch: PLAY only, no BACK)
            SetTutorialBackVisible(false);
            tutorialPanel.SetActive(true);
            startPanel.SetActive(false);

            // Mark that the first run has been completed
            PlayerPrefs.SetInt(FirstRunKey, 1);
            PlayerPrefs.Save();
        }
        else
        {
            // Hide the tutorial panel
            player.SetActive(true);
            startPanel.SetActive(false);
            enemySpawner.StartSpawning();
            drainableSpawner.StartSpawning();
        }
    }

    // "How to play" on the main screen: same panels as the first-run tutorial.
    // Its button starts the run, just like after the first-run tutorial.
    public void ShowTutorial()
    {
        SetTutorialBackVisible(true);
        tutorialPanel.SetActive(true);
        startPanel.SetActive(false);
    }

    // BACK on How to play: return to the main screen without starting a run.
    public void BackToMenu()
    {
        tutorialPanel.SetActive(false);
        startPanel.SetActive(true);
    }

    private void SetTutorialBackVisible(bool visible)
    {
        HowToPlayScreen screen = tutorialPanel != null ? tutorialPanel.GetComponent<HowToPlayScreen>() : null;
        if (screen != null)
            screen.SetBackVisible(visible);
    }

    public void CloseTutorial()
    {
        tutorialPanel.SetActive(false);
    }
}
