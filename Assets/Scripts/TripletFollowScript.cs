using UnityEngine;

public class PlayerTrackingProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 5f;
    public float lifetime = 3f;

    private Vector2 direction;
    private bool hasInitialized = false;

    private void Start()
    {
        // Find the player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            // Calculate direction to the player
            direction = (playerObj.transform.position - transform.position).normalized;
            hasInitialized = true;
        }
        else
        {
            // Destroy if no player found
            Destroy(gameObject);
            return;
        }

        // Destroy the projectile after lifetime
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Only move if direction has been initialized
        if (hasInitialized)
        {
            transform.position += (Vector3)direction * speed * Time.deltaTime;
        }
    }
}