using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwarmerScript : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float damage = 1f; // Damage amount

    private Rigidbody2D rb;
    private PlayerAwareness playerAwareness;
    private Vector2 targetDirection;
    public EnemyHealthScript enemyHealthScript;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerAwareness = GetComponent<PlayerAwareness>();
        enemyHealthScript = GetComponent<EnemyHealthScript>();
    }

    private void FixedUpdate()
    {
        UpdateTargetDirection();
        RotateTowardsTarget();
        SetVelocity();
    }

    private void UpdateTargetDirection()
    {
        targetDirection = playerAwareness.DirectionToPlayer;
    }

    private void RotateTowardsTarget()
    {
        Quaternion targetRotation = Quaternion.LookRotation(transform.forward, targetDirection);
        Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        rb.SetRotation(rotation);
    }

    private void SetVelocity()
    {
        rb.velocity = transform.up * speed;
    }

    // Damage player when Swarmer collides
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) // Ensure the Player has the "Player" tag
        {
            PlayerLivesScript playerLives = other.GetComponent<PlayerLivesScript>(); // Get PlayerLivesScript
            if (playerLives != null)
            {
                playerLives.TakeDamage(damage); // Apply damage
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player Bullet"))
        {
            Debug.Log("Bullet hit! Destroying Swarmer...");
            if (collision.gameObject.CompareTag("Player Bullet"))
            { 
                enemyHealthScript.TakeDamage(10);  // Reduce health
                Destroy(collision.gameObject);      // Destroy the bullet
            }
        }

        if (collision.gameObject.CompareTag("Player")) // Ensure the Player has the "Player" tag
        {
            PlayerLivesScript playerLives = collision.gameObject.GetComponent<PlayerLivesScript>(); // Get PlayerLivesScript
            if (playerLives != null)
            {
                playerLives.TakeDamage(damage); // Apply damage
            }
        }
    }
}
