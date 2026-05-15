using UnityEngine;

public class PlayerDamageScript : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 1f;
    private bool hasHit = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHit) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            hasHit = true;
            PlayerLivesScript playerLives = collision.gameObject.GetComponent<PlayerLivesScript>();
            if (playerLives != null)
            {
                playerLives.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
    }
}