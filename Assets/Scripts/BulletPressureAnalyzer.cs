using System.Collections.Generic;
using UnityEngine;

public class BulletPressureAnalyzer : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public PlayerPerformanceTelemetry telemetry;

    [Header("Pressure Settings")]
    public string enemyBulletTag = "Enemy Bullet";
    public float playerMoveSpeed = 6f;
    public float dangerRadius = 1.1f;
    public float nearMissRadius = 1.8f;
    public float lookaheadSeconds = 1.25f;
    public int expectedBulletCapacity = 120;

    private readonly HashSet<int> reportedNearMisses = new HashSet<int>();

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (telemetry == null && player != null)
            telemetry = player.GetComponent<PlayerPerformanceTelemetry>();
    }

    public float GetPressureScore()
    {
        if (player == null)
            return 0f;

        GameObject[] bullets = GameObject.FindGameObjectsWithTag(enemyBulletTag);
        if (bullets.Length == 0)
            return 0f;

        float immediateDanger = 0f;
        float projectedDanger = 0f;

        foreach (GameObject bullet in bullets)
        {
            if (bullet == null)
                continue;

            Vector2 bulletPosition = bullet.transform.position;
            Vector2 playerPosition = player.position;
            Vector2 toPlayer = playerPosition - bulletPosition;
            float distance = toPlayer.magnitude;

            if (distance <= nearMissRadius)
                ReportNearMissOnce(bullet);

            immediateDanger += Mathf.Clamp01(1f - (distance / Mathf.Max(0.1f, dangerRadius * 3f)));

            Vector2 velocity = GetBulletVelocity(bullet);
            if (velocity.sqrMagnitude <= 0.01f)
                continue;

            Vector2 bulletDirection = velocity.normalized;
            float closingSpeed = Vector2.Dot(bulletDirection, toPlayer.normalized) * velocity.magnitude;
            if (closingSpeed <= 0f)
                continue;

            float timeToClosest = Mathf.Clamp(Vector2.Dot(toPlayer, velocity) / velocity.sqrMagnitude, 0f, lookaheadSeconds);
            Vector2 closestPoint = bulletPosition + velocity * timeToClosest;
            float closestDistance = Vector2.Distance(playerPosition, closestPoint);
            float escapeDistance = playerMoveSpeed * timeToClosest;
            float survivabilityMargin = closestDistance + escapeDistance;
            projectedDanger += Mathf.Clamp01(1f - (survivabilityMargin / Mathf.Max(0.1f, dangerRadius * 2f)));
        }

        float bulletDensity = Mathf.Clamp01((float)bullets.Length / Mathf.Max(1, expectedBulletCapacity));
        float averageImmediate = Mathf.Clamp01(immediateDanger / Mathf.Max(1, bullets.Length));
        float averageProjected = Mathf.Clamp01(projectedDanger / Mathf.Max(1, bullets.Length));

        return Mathf.Clamp01((bulletDensity * 0.35f) + (averageImmediate * 0.25f) + (averageProjected * 0.4f));
    }

    private Vector2 GetBulletVelocity(GameObject bullet)
    {
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null && rb.velocity.sqrMagnitude > 0.01f)
            return rb.velocity;

        EnemyProjectileScript projectile = bullet.GetComponent<EnemyProjectileScript>();
        if (projectile != null)
            return (Vector2)bullet.transform.up * projectile.speed;

        return Vector2.zero;
    }

    private void ReportNearMissOnce(GameObject bullet)
    {
        if (telemetry == null)
            return;

        int id = bullet.GetInstanceID();
        if (reportedNearMisses.Add(id))
            telemetry.ReportNearMiss();
    }

    /// <summary>
    /// Clears the near-miss tracking set. Call this at the start of each episode
    /// so that reused GetInstanceID() values from previously destroyed bullets
    /// cannot suppress near-miss events for newly spawned ones.
    /// </summary>
    public void ClearEpisode()
    {
        reportedNearMisses.Clear();
    }
}
