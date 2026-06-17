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
    [Tooltip("Use hardcoded approach/retreat movement. Disable this for PPO-controlled movement training.")]
    public bool useScriptedMovement = true;
    [Tooltip("Automatically disable scripted movement when an EnemyAgent is attached.")]
    public bool disableScriptedMovementWhenAgentPresent = true;

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
    private PlayerPerformanceTelemetry playerTelemetry;
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
        // BUG-11 fix: all base* values are already set in Awake(); duplicating them
        // here was a misleading no-op and would silently override any Awake()-to-Start()
        // changes. Only post-component logic belongs here.
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
        if (disableScriptedMovementWhenAgentPresent && enemyAgent != null)
        {
            useScriptedMovement = false;
        }

        trainingManager = FindObjectOfType<RLTrainingManager>();

        // Cache player telemetry for shot-hit reporting
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTelemetry = playerObj.GetComponent<PlayerPerformanceTelemetry>();

        DanmakuDDAController.EnsureExists().RegisterTunable(this);
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        if (useScriptedMovement)
        {
            HandleScriptedMovement();
        }

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

    private void HandleScriptedMovement()
    {
        float distance = Vector2.Distance(transform.position, player.position);

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
    }

    /// <summary>
    /// Fire spread of projectiles and report shots fired to the training manager
    /// </summary>
    private void ShootSpread()
    {
        // BUG-12 fix: guard against unassigned projectile prefab to avoid NullReferenceException.
        if (projectile == null)
        {
            Debug.LogWarning("[TestEnemyScript] projectile prefab is not assigned!");
            return;
        }

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
            
            // Set bullet speed and inject cached references so bullet Start()
            // never needs FindObjectOfType (BUG-09 / BUG-10 fix).
            EnemyProjectileScript projectileScript = bullet.GetComponent<EnemyProjectileScript>();
            if (projectileScript != null)
            {
                projectileScript.speed = bulletSpeed;
                projectileScript.enemyAgent = enemyAgent;
                projectileScript.trainingManager = trainingManager; // BUG-10: inject at spawn
            }

            // Report shot fired for each instantiated projectile
            if (trainingManager != null)
                trainingManager.ReportEnemyShotFired();
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

            // Report the hit to the player telemetry so accuracy is logged correctly
            if (playerTelemetry == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    playerTelemetry = playerObj.GetComponent<PlayerPerformanceTelemetry>();
            }
            if (playerTelemetry != null)
                playerTelemetry.ReportShotHit(damagePerShot);

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
