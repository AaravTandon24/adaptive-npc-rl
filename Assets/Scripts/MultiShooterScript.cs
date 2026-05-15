using UnityEngine;

public class MultiShooterScript : MonoBehaviour
{
    public float speed;
    public float stoppingDistance;
    public float retreatDistance;
    public float timeBtwShots;
    public float startTimeBtwShots;
    public GameObject projectile;
    private Transform player;
    private EnemyHealthScript enemyHealthScript;  // Reference to enemy health

    void Start()
    {
        FindPlayer();
        timeBtwShots = startTimeBtwShots;
        enemyHealthScript = GetComponent<EnemyHealthScript>();  // Get health script from the enemy itself
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance > stoppingDistance)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.deltaTime);
        }
        else if (distance < retreatDistance)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, -speed * Time.deltaTime);
        }

        // Flip enemy to face player
        FlipEnemy();

        if (timeBtwShots <= 0)
        {
            // Calculate direction towards the player
            Vector2 direction = (player.position - transform.position).normalized;

            // Calculate the angle to rotate the projectile
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            // Instantiate projectile with calculated rotation
            Instantiate(projectile, transform.position, rotation);
            timeBtwShots = startTimeBtwShots;
        }
        else
        {
            timeBtwShots -= Time.deltaTime;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player Bullet"))
        {
            enemyHealthScript.TakeDamage(10);  // Reduce health
            Destroy(collision.gameObject);      // Destroy the bullet
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void FlipEnemy()
    {
        if (player != null)
        {
            if (player.position.x > transform.position.x)
                transform.localScale = new Vector3(-1, 1, 1);  // Flip right
            else
                transform.localScale = new Vector3(1, 1, 1);   // Flip left
        }
    }


}
