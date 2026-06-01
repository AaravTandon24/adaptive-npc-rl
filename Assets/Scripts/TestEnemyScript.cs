using UnityEngine;

/// <summary>
/// Test enemy script for Reinforcement Learning research.
/// This enemy has 10 HP and takes 10 damage per shot.
/// Does not modify base game scripts.
/// </summary>
public class TestEnemyScript : MonoBehaviour, IDifficultyTunable
{
    [Header("Movement Settings")]
    public float stoppingDistance = 3f;
    public float retreatDistance = 2f;
    public float minSafeDistance = 1.5f;  // Minimum safe distance from player

    [Header("Shooting Settings")]
    public GameObject projectile;

    [Header("Health Settings")]
    public float maxHealth = 10f;
    public float damagePerShot = 10f;

    [Header("Difficulty Settings")]
    [Tooltip("Shots per second")]
    public float fireRate = 0.5f;
    
    [Tooltip("Speed of enemy movement")]
    public float movementSpeed = 2f;
    
    [Tooltip("Speed of bullets")]
    public float bulletSpeed = 5f;
    
    [Tooltip("Angle spread for 3-shot burst (degrees)")]
    public float spreadAngle = 30f;

    [Tooltip("Base number of bullets fired per burst")]
    public int baseBulletsPerBurst = 3;

    private Transform player;
    private TestEnemyHealthScript enemyHealthScript;
    private EnemyAgent enemyAgent;
    private RLTrainingManager trainingManager;
    private float currentTimeBtwShots;
    private float baseFireRate;
    private float baseMovementSpeed;
    private float baseBulletSpeed;
    private float baseSpreadAngle;
    private int currentBulletsPerBurst;

    private void Awake()
    {
        baseFireRate = fireRate;
        baseMovementSpeed = movementSpeed;
        baseBulletSpeed = bulletSpeed;
        baseSpreadAngle = spreadAngle;
        baseBulletsPerBurst = 3;
        currentBulletsPerBurst = baseBulletsPerBurst;
    }

    void Start()
    {
        baseFireRate = fireRate;
        baseMovementSpeed = movementSpeed;
        baseBulletSpeed = bulletSpeed;
        baseSpreadAngle = spreadAngle;
        baseBulletsPerBurst = 3;
        currentBulletsPerBurst = baseBulletsPerBurst;

        FindPlayer();
        currentTimeBtwShots = (fireRate > 0f) ? 1f / fireRate : 1f; // guard against zero
        
        // Get or add TestEnemyHealthScript
        enemyHealthScript = GetComponent<TestEnemyHealthScript>();
        if (enemyHealthScript == null)
        {
            enemyHealthScript = gameObject.AddComponent<TestEnemyHealthScript>();
        }
        
        // Configure health for testing (10 HP)
        enemyHealthScript.maxHealth = maxHealth;
        enemyHealthScript.currentHealth = maxHealth;
        enemyAgent = GetComponent<EnemyAgent>();
        trainingManager = FindObjectOfType<RLTrainingManager>();

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

        // Movement logic
        // Retreat when closer than retreatDistance
        if (distance < retreatDistance)
        {
            // Move away from player using a proper direction vector
            Vector2 awayDir = (transform.position - player.position).normalized;
            transform.position += (Vector3)(awayDir * movementSpeed * Time.deltaTime);
        }
        // Approach when further than stoppingDistance
        else if (distance > stoppingDistance)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, movementSpeed * Time.deltaTime);
        }
        // Otherwise stay in place

        // Shooting logic with fireRate and 3-shot spread
        if (currentTimeBtwShots <= 0)
        {
            ShootSpread();
            currentTimeBtwShots = (fireRate > 0f) ? 1f / fireRate : 1f; // Reset timer based on fireRate
        }
        else
        {
            currentTimeBtwShots -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Fire 3 projectiles with spread angle
    /// </summary>
    private void ShootSpread()
    {
        if (!CanFireEnemyBullet())
            return;

        // Calculate base direction to player
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        float baseAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;

        int bulletsToFire = Mathf.Max(1, currentBulletsPerBurst);
        
        for (int i = 0; i < bulletsToFire; i++)
        {
            float offset = bulletsToFire == 1 ? 0f : Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, (float)i / (bulletsToFire - 1));
            float bulletAngle = baseAngle + offset;
            Quaternion bulletRotation = Quaternion.Euler(0f, 0f, bulletAngle - 90f); // -90 to align with Unity's up direction
            
            // Instantiate projectile
            GameObject bullet = Instantiate(projectile, transform.position, bulletRotation);
            
            // Set bullet speed if it has EnemyProjectileScript
            EnemyProjectileScript projectileScript = bullet.GetComponent<EnemyProjectileScript>();
            if (projectileScript != null)
            {
                projectileScript.speed = bulletSpeed;
            }
        }
    }

    public void ApplyDifficulty(DifficultyProfile profile)
    {
        fireRate = Mathf.Max(0.05f, baseFireRate * profile.fireRateMultiplier);
        movementSpeed = Mathf.Max(0.1f, baseMovementSpeed * profile.enemySpeedMultiplier);
        bulletSpeed = Mathf.Max(0.1f, baseBulletSpeed * profile.bulletSpeedMultiplier);
        spreadAngle = Mathf.Max(1f, baseSpreadAngle * profile.spreadAngleMultiplier);
        currentBulletsPerBurst = Mathf.Clamp(Mathf.RoundToInt(baseBulletsPerBurst * profile.bulletCountMultiplier), 3, 6);
    }

    private bool CanFireEnemyBullet()
    {
        if (DanmakuDDAController.Instance == null)
            return true;

        int activeBullets = GameObject.FindGameObjectsWithTag("Enemy Bullet").Length;
        return activeBullets < DanmakuDDAController.Instance.CurrentProfile.maxActiveEnemyBullets;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player Bullet"))
        {
            // Take 10 damage per shot for testing
            if (enemyAgent != null)
                enemyAgent.TakeDamage(damagePerShot);
            else
                enemyHealthScript.TakeDamage(damagePerShot);

            if (trainingManager != null)
                trainingManager.ReportPlayerDamageDealt(damagePerShot);

            Destroy(collision.gameObject);
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
