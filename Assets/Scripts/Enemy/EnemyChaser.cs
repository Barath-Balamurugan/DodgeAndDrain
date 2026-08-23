using UnityEngine;

public class EnemyChaser : EnemyBase
{
    private Transform playerTransform;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
        {
            playerTransform = GameManager.Instance.player.transform;
        }
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
        if (playerTransform == null || GameManager.Instance == null || GameManager.Instance.IsGameOver)
            return;

        Vector3 dir = (playerTransform.position - transform.position).normalized;
        transform.position += dir * Time.deltaTime * moveSpeed; // Chasing speed is
    }
}
