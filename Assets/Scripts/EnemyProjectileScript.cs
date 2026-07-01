using UnityEngine;

public class EnemyProjectileScript : MonoBehaviour, IDifficultyTunable
{
    [Header("Projectile Settings")]
    public float speed = 5f;
    public float lifetime = 1.8f;
    public float damage = 1f;

    private Vector2 direction;
    private bool hasHit = false;
    private float baseSpeed;
    // BUG-10 fix: public so TestEnemyScript.ShootSpread() can inject the cached
    // reference at spawn time, removing the need for FindObjectOfType in Start().
    public RLTrainingManager trainingManager;
    public EnemyAgent enemyAgent;

    private void Awake()
    {
        baseSpeed = speed;
    }

    private void Start()
    {
        baseSpeed = speed;

        if (DanmakuDDAController.Instance != null)
            DanmakuDDAController.Instance.RegisterTunable(this);

        // BUG-09 / BUG-10 fix: do NOT call FindObjectOfType here.
        // TestEnemyScript.ShootSpread() already assigns both enemyAgent and
        // trainingManager at instantiation time. Performing a full scene search
        // inside every bullet's Start() (potentially dozens per second at max
        // fire rate) was a significant performance drain with no benefit.
        if (trainingManager == null)
            Debug.LogWarning("[EnemyProjectileScript] trainingManager not injected at spawn — shot-hit stats will not be recorded.");

        // Use the bullet's initial rotation direction instead of always aiming at player
        // This allows for spread shots from TestEnemyScript
        direction = transform.up.normalized;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += (Vector3)direction * speed * Time.deltaTime;
    }

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

            if (trainingManager != null)
            {
                trainingManager.ReportEnemyDamageDealt(damage);
                // Report that an enemy shot hit the player
                trainingManager.ReportEnemyShotHit();
            }

            if (enemyAgent != null)
                enemyAgent.RewardForHit();

            DestroyProjectile();
        }
    }

    private void DestroyProjectile()
    {
        Destroy(gameObject);
    }

    public void ApplyDifficulty(DifficultyProfile profile)
    {
        speed = Mathf.Max(0.1f, baseSpeed * profile.bulletSpeedMultiplier);
    }
}
