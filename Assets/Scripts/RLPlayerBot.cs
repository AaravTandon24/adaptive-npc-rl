using UnityEngine;

/// <summary>
/// Scripted player bot for RL training. Automatically moves and shoots so the
/// PPO enemy agent always has a consistent, active threat to learn from.
///
/// Attach this to the Player GameObject alongside PlayerMovement and Shooting.
/// It ONLY activates when an RLTrainingManager is present in the scene, so it
/// never interferes with normal gameplay. When active, it disables PlayerMovement
/// and takes over movement itself.
///
/// Movement strategy — orbit + strafe:
///   - Approaches the enemy to a preferred engagement distance.
///   - Strafes laterally (orbits) while at that distance.
///   - Retreats if the enemy closes in too far.
///   - Randomly reverses strafe direction every few seconds so the pattern varies.
///   - Stays inside arena bounds at all times.
/// </summary>
public class RLPlayerBot : MonoBehaviour
{
    [Header("Engagement Range")]
    [Tooltip("Preferred distance to keep from the enemy")]
    public float preferredDistance = 4f;
    [Tooltip("Distance below which the bot retreats")]
    public float retreatDistance = 2.5f;

    [Header("Movement")]
    [Tooltip("Movement speed — should match PlayerMovement.moveSpeed")]
    public float moveSpeed = 7f;
    [Tooltip("How often (seconds) the strafe direction randomly reverses")]
    public float strafeFlipInterval = 2.5f;

    [Header("Arena Bounds")]
    public Vector2 arenaMin = new Vector2(-8f, -4.5f);
    public Vector2 arenaMax = new Vector2(8f, 4.5f);

    // ── cached refs ──────────────────────────────────────────────────────────
    private Rigidbody2D rb;
    private Shooting shooting;
    private PlayerMovement playerMovement;
    private Transform enemy;

    // ── internal state ───────────────────────────────────────────────────────
    private float strafeDirection = 1f;     // +1 = counter-clockwise, -1 = clockwise
    private float nextStrafeFlip;
    private bool botActive = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Only activate when an RLTrainingManager is in the scene.
        if (FindObjectOfType<RLTrainingManager>() == null)
        {
            enabled = false;
            return;
        }

        rb             = GetComponent<Rigidbody2D>();
        shooting       = GetComponent<Shooting>();
        playerMovement = GetComponent<PlayerMovement>();

        // Hand over movement control to this bot.
        if (playerMovement != null)
            playerMovement.enabled = false;

        nextStrafeFlip = Time.time + strafeFlipInterval;
        botActive = true;

        Debug.Log("[RLPlayerBot] Active — human input disabled for this session.");
    }

    void OnEnable()
    {
        // Re-disable PlayerMovement every time the player is re-enabled after
        // an episode reset (RLTrainingManager calls player.SetActive(true) each episode).
        if (!botActive) return;
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
            playerMovement.enabled = false;
    }

    void Update()
    {
        if (!botActive) return;

        // Lazily find the enemy (it may not exist on the first frame).
        if (enemy == null)
        {
            GameObject enemyGO = GameObject.FindGameObjectWithTag("Enemy");
            if (enemyGO != null) enemy = enemyGO.transform;
            return;
        }

        // ── Auto-shoot: face enemy and fire ──────────────────────────────────
        AimAtEnemy();
        if (shooting != null)
            shooting.TryShoot();

        // ── Randomly flip strafe direction ────────────────────────────────────
        if (Time.time >= nextStrafeFlip)
        {
            strafeDirection = Random.value > 0.5f ? 1f : -1f;
            nextStrafeFlip  = Time.time + strafeFlipInterval + Random.Range(-0.5f, 0.5f);
        }
    }

    void FixedUpdate()
    {
        if (!botActive || enemy == null || rb == null) return;

        Vector2 toEnemy   = (Vector2)enemy.position - rb.position;
        float   dist      = toEnemy.magnitude;
        Vector2 dirToEnemy = dist > 0.001f ? toEnemy / dist : Vector2.up;

        // Perpendicular to the enemy direction (for orbiting / strafing)
        Vector2 perpendicular = new Vector2(-dirToEnemy.y, dirToEnemy.x) * strafeDirection;

        Vector2 desiredVelocity;

        if (dist < retreatDistance)
        {
            // Too close — back away directly
            desiredVelocity = -dirToEnemy * moveSpeed;
        }
        else if (dist > preferredDistance + 1f)
        {
            // Too far — close in with a slight strafe component
            desiredVelocity = (dirToEnemy * 0.8f + perpendicular * 0.2f).normalized * moveSpeed;
        }
        else
        {
            // At good range — orbit (mostly lateral, small approach/retreat component)
            float radialBias = (dist - preferredDistance) * 0.4f; // positive → move in
            desiredVelocity  = (perpendicular + dirToEnemy * radialBias).normalized * moveSpeed;
        }

        // Clamp to arena before applying
        Vector2 newPos = rb.position + desiredVelocity * Time.fixedDeltaTime;
        newPos.x = Mathf.Clamp(newPos.x, arenaMin.x + 0.5f, arenaMax.x - 0.5f);
        newPos.y = Mathf.Clamp(newPos.y, arenaMin.y + 0.5f, arenaMax.y - 0.5f);

        rb.MovePosition(newPos);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Rotate the player to face the enemy so bullets travel toward it.</summary>
    private void AimAtEnemy()
    {
        if (enemy == null || rb == null) return;
        Vector2 lookDir = (Vector2)enemy.position - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;
        rb.rotation = angle;
    }
}
