using UnityEngine;

public class EnemyScript : MonoBehaviour, IDifficultyTunable
{
    public float speed;
    public float stoppingDistance;
    public float retreatDistance;

    public float timeBtwShots;
    public float startTimeBtwShots;

    public GameObject projectile;
    private Transform player;
    private float baseSpeed;
    private float baseStartTimeBtwShots;

    private EnemyHealthScript enemyHealthScript;  // Reference to enemy health
    private EnemyAgent enemyAgent;

    private void Awake()
    {
        baseSpeed = speed;
        baseStartTimeBtwShots = startTimeBtwShots;
    }

    void Start()
    {
        baseSpeed = speed;
        baseStartTimeBtwShots = startTimeBtwShots;
        FindPlayer();
        timeBtwShots = startTimeBtwShots;
        enemyHealthScript = GetComponent<EnemyHealthScript>();  // Get health script from the enemy itself
        enemyAgent = GetComponent<EnemyAgent>();

        DanmakuDDAController.EnsureExists().RegisterTunable(this);
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
            if (CanFireEnemyBullet())
            {
                GameObject bullet = Instantiate(projectile, transform.position, Quaternion.identity);
                EnemyProjectileScript projectileScript = bullet.GetComponent<EnemyProjectileScript>();
                if (projectileScript != null)
                {
                    projectileScript.enemyAgent = enemyAgent;
                }
            }

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

    public void ApplyDifficulty(DifficultyProfile profile)
    {
        speed = Mathf.Max(0.1f, baseSpeed * profile.enemySpeedMultiplier);
        startTimeBtwShots = Mathf.Max(0.05f, baseStartTimeBtwShots / profile.fireRateMultiplier);
    }

    private bool CanFireEnemyBullet()
    {
        if (DanmakuDDAController.Instance == null)
            return true;

        int activeBullets = GameObject.FindGameObjectsWithTag("Enemy Bullet").Length;
        return activeBullets < DanmakuDDAController.Instance.CurrentProfile.maxActiveEnemyBullets;
    }
}
