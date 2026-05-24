using UnityEngine;

public class EnemyProjectileScript : MonoBehaviour, IDifficultyTunable
{
    [Header("Projectile Settings")]
    public float speed = 5f;
    public float lifetime = 3f;
    public float damage = 1f;

    private Vector2 direction;
    private bool hasHit = false;
    private float baseSpeed;

    private void Awake()
    {
        baseSpeed = speed;
    }

    private void Start()
    {
        baseSpeed = speed;

        if (DanmakuDDAController.Instance != null)
            DanmakuDDAController.Instance.RegisterTunable(this);

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
