using UnityEngine;

public class EnemyScript : MonoBehaviour
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

        if (timeBtwShots <= 0)
        {
            Instantiate(projectile, transform.position, Quaternion.identity);
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
}
