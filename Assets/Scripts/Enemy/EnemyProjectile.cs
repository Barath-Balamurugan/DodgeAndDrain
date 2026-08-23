using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
            return;

        if (other.GetComponent<PlayerMovement2D>() != null)
        {
            GameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }
}
