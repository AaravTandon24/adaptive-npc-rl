using UnityEngine;

/// <summary>
/// Human-like scripted player bot for RL training.
///
/// Simulates a real player's behaviour:
///   - Movement: reactive state machine (approach / strafe / retreat / dodge)
///   - Aiming:   smooth rotation with slight imperfection and lead prediction
///   - Shooting: burst-fire (2–5 shots) followed by a human-reaction pause
///
/// Activates ONLY when an RLTrainingManager is present — never interferes with
/// normal gameplay.  Attach alongside PlayerMovement and Shooting on the Player.
/// </summary>
public class RLPlayerBot : MonoBehaviour
{
    // ── Engagement distances ─────────────────────────────────────────────────
    [Header("Engagement Range")]
    [Tooltip("Preferred fighting distance from the enemy")]
    public float preferredDistance  = 4.5f;
    [Tooltip("Below this distance the bot retreats")]
    public float retreatDistance    = 2.5f;
    [Tooltip("Above this distance the bot approaches")]
    public float approachDistance   = 6.0f;

    // ── Movement ─────────────────────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("Speed — keep in sync with PlayerMovement.moveSpeed")]
    public float moveSpeed          = 7f;
    [Tooltip("How long the bot keeps strafing in one direction before reconsidering")]
    public float strafeDecisionTime = 1.2f;
    [Tooltip("How long a dodge lasts")]
    public float dodgeDuration      = 0.4f;
    [Tooltip("Minimum distance an enemy bullet must be to trigger a dodge")]
    public float dodgeTriggerRadius = 3.5f;

    // ── Aiming ───────────────────────────────────────────────────────────────
    [Header("Aiming")]
    [Tooltip("Rotation speed toward target (degrees/sec) — humans aren't instant")]
    public float aimSpeed           = 280f;
    [Tooltip("Max angular jitter added to simulate human imprecision (degrees)")]
    public float aimJitter          = 6f;
    [Tooltip("How far ahead of the enemy's movement to lead-aim (0 = no lead)")]
    public float leadPrediction     = 0.12f;

    // ── Burst firing ─────────────────────────────────────────────────────────
    [Header("Burst Firing")]
    [Tooltip("Minimum shots in a burst")]
    public int   burstMin           = 2;
    [Tooltip("Maximum shots in a burst")]
    public int   burstMax           = 5;
    [Tooltip("Delay between individual shots within a burst")]
    public float inBurstDelay       = 0.08f;
    [Tooltip("Pause between bursts (min)")]
    public float burstPauseMin      = 0.35f;
    [Tooltip("Pause between bursts (max)")]
    public float burstPauseMax      = 1.1f;
    [Tooltip("Only shoot when aimed within this angle of the enemy")]
    public float fireAngleThreshold = 20f;

    // ── Arena ────────────────────────────────────────────────────────────────
    [Header("Arena Bounds")]
    public Vector2 arenaMin         = new Vector2(-8f, -4.5f);
    public Vector2 arenaMax         = new Vector2(8f,  4.5f);

    // ── Cached refs ──────────────────────────────────────────────────────────
    private Rigidbody2D    rb;
    private Shooting       shooting;
    private PlayerMovement playerMovement;
    private Transform      enemy;
    private Rigidbody2D    enemyRb;
    private RLTrainingManager trainingManager;

    // ── Movement state ───────────────────────────────────────────────────────
    private enum MoveState { Approach, Strafe, Retreat, Dodge }
    private MoveState   state             = MoveState.Strafe;
    private float       strafeDir         = 1f;      // +1 / -1
    private float       nextStateDecision = 0f;
    private Vector2     dodgeDir          = Vector2.zero;
    private float       dodgeEndTime      = 0f;

    // ── Aim state ────────────────────────────────────────────────────────────
    private float currentAimAngle = 0f;
    private float currentJitter   = 0f;
    private float nextJitterTime  = 0f;

    // ── Burst-fire state ─────────────────────────────────────────────────────
    private int   shotsLeftInBurst = 0;
    private float nextShotTime     = 0f;
    private bool  inPause          = false;

    private bool botActive = false;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        trainingManager = FindObjectOfType<RLTrainingManager>();
        if (trainingManager == null)
        {
            enabled = false;
            return;
        }

        rb             = GetComponent<Rigidbody2D>();
        shooting       = GetComponent<Shooting>();
        playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement != null)
            playerMovement.enabled = false;

        botActive = true;
        BeginNewBurst();
        Debug.Log("[RLPlayerBot] Active — human-like bot controls this player.");
    }

    void OnEnable()
    {
        // Re-disable human movement after each episode re-enable.
        if (!botActive) return;
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null) playerMovement.enabled = false;
    }

    /// <summary>
    /// Resolves the enemy Transform via RLTrainingManager so no tag is needed.
    /// </summary>
    private void RefreshEnemyRef()
    {
        if (trainingManager == null) return;
        GameObject enemyGO = trainingManager.enemy;
        if (enemyGO != null && (enemy == null || enemy.gameObject != enemyGO))
        {
            enemy   = enemyGO.transform;
            enemyRb = enemyGO.GetComponent<Rigidbody2D>();
        }
    }

    void Update()
    {
        if (!botActive) return;

        // Force PlayerMovement component to stay disabled so it does not fight the bot.
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null && playerMovement.enabled)
        {
            playerMovement.enabled = false;
        }

        // Always refresh from the manager — enemy reference is stable but safe to re-check.
        RefreshEnemyRef();

        if (enemy == null || !enemy.gameObject.activeInHierarchy) return;

        UpdateAim();
        UpdateFiring();
    }

    void FixedUpdate()
    {
        if (!botActive || enemy == null || rb == null) return;
        UpdateMovement();
    }

    // ── Aim ──────────────────────────────────────────────────────────────────

    void UpdateAim()
    {
        // Lead prediction: aim slightly ahead of where the enemy is moving.
        Vector2 targetPos = (Vector2)enemy.position;
        if (enemyRb != null && enemyRb.velocity.sqrMagnitude > 0.1f)
            targetPos += enemyRb.velocity * leadPrediction;

        Vector2 toTarget = targetPos - rb.position;
        float   wantedAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;

        // Refresh aim jitter periodically
        if (Time.time >= nextJitterTime)
        {
            currentJitter  = Random.Range(-aimJitter, aimJitter);
            nextJitterTime = Time.time + Random.Range(0.1f, 0.25f);
        }
        wantedAngle += currentJitter;

        // Smoothly rotate toward the wanted angle
        currentAimAngle = Mathf.MoveTowardsAngle(currentAimAngle, wantedAngle,
                                                  aimSpeed * Time.deltaTime);
        rb.rotation = currentAimAngle;
    }

    // ── Burst firing ─────────────────────────────────────────────────────────

    void UpdateFiring()
    {
        if (Time.time < nextShotTime) return;

        if (inPause)
        {
            // Pause over — start the next burst
            BeginNewBurst();
            return;
        }

        // Only fire when roughly aimed at the enemy
        if (shooting != null && IsAimedAtEnemy())
        {
            shooting.TryShoot();
            shotsLeftInBurst--;

            if (shotsLeftInBurst <= 0)
            {
                // Burst exhausted — begin pause
                inPause     = true;
                nextShotTime = Time.time + Random.Range(burstPauseMin, burstPauseMax);
            }
            else
            {
                // Fire next shot in burst after a short in-burst delay
                nextShotTime = Time.time + inBurstDelay + Random.Range(0f, 0.04f);
            }
        }
    }

    void BeginNewBurst()
    {
        inPause          = false;
        shotsLeftInBurst = Random.Range(burstMin, burstMax + 1);
        // Small reaction-time delay before the burst begins
        nextShotTime     = Time.time + Random.Range(0.05f, 0.18f);
    }

    bool IsAimedAtEnemy()
    {
        if (enemy == null) return false;
        Vector2 toEnemy = (Vector2)enemy.position - rb.position;
        // rb.rotation is already set; transform.up is the firing direction.
        float angle = Vector2.Angle(transform.up, toEnemy);
        return angle <= fireAngleThreshold;
    }

    // ── Movement ─────────────────────────────────────────────────────────────

    void UpdateMovement()
    {
        Vector2 toEnemy    = (Vector2)enemy.position - rb.position;
        float   dist       = toEnemy.magnitude;
        Vector2 dirToEnemy = dist > 0.001f ? toEnemy / dist : Vector2.up;
        Vector2 perpendicular = new Vector2(-dirToEnemy.y, dirToEnemy.x);

        // ── Dodge check: highest priority ────────────────────────────────────
        if (Time.time < dodgeEndTime)
        {
            ApplyVelocity(dodgeDir * moveSpeed);
            return;
        }

        // Look for the nearest incoming enemy bullet
        GameObject threat = FindNearestIncomingBullet();
        if (threat != null)
        {
            // Dodge perpendicular to the bullet's velocity
            Vector2 bulletVel = threat.GetComponent<Rigidbody2D>()?.velocity ?? Vector2.zero;
            if (bulletVel.sqrMagnitude > 0.1f)
            {
                Vector2 bDir  = bulletVel.normalized;
                Vector2 perp1 = new Vector2(-bDir.y,  bDir.x);
                Vector2 perp2 = new Vector2( bDir.y, -bDir.x);
                // Pick the dodge side that moves away from the bullet
                Vector2 toBullet = (Vector2)threat.transform.position - rb.position;
                dodgeDir    = Vector2.Dot(perp1, toBullet) < 0 ? perp1 : perp2;
                dodgeEndTime = Time.time + dodgeDuration;
                state        = MoveState.Dodge;
                ApplyVelocity(dodgeDir * moveSpeed);
                return;
            }
        }

        // ── State decision (re-evaluate periodically) ─────────────────────────
        if (Time.time >= nextStateDecision)
        {
            if (dist < retreatDistance)
                state = MoveState.Retreat;
            else if (dist > approachDistance)
                state = MoveState.Approach;
            else
                state = MoveState.Strafe;

            // Occasionally reverse strafe direction
            if (state == MoveState.Strafe && Random.value < 0.35f)
                strafeDir = -strafeDir;

            nextStateDecision = Time.time + strafeDecisionTime + Random.Range(-0.3f, 0.3f);
        }

        // ── Execute state ─────────────────────────────────────────────────────
        Vector2 desired;
        switch (state)
        {
            case MoveState.Approach:
                // Close in with a small strafe component so motion isn't perfectly linear
                desired = (dirToEnemy * 0.75f + perpendicular * strafeDir * 0.25f).normalized;
                break;

            case MoveState.Retreat:
                desired = (-dirToEnemy * 0.8f + perpendicular * strafeDir * 0.2f).normalized;
                break;

            case MoveState.Strafe:
            default:
                // Orbit at preferred range — mostly perpendicular, small radial correction
                float radialPull = Mathf.Clamp((dist - preferredDistance) * 0.5f, -1f, 1f);
                desired = (perpendicular * strafeDir + dirToEnemy * radialPull).normalized;
                break;
        }

        ApplyVelocity(desired * moveSpeed);
    }

    void ApplyVelocity(Vector2 vel)
    {
        Vector2 newPos = rb.position + vel * Time.fixedDeltaTime;
        // Clamp inside arena
        newPos.x = Mathf.Clamp(newPos.x, arenaMin.x + 0.4f, arenaMax.x - 0.4f);
        newPos.y = Mathf.Clamp(newPos.y, arenaMin.y + 0.4f, arenaMax.y - 0.4f);
        rb.MovePosition(newPos);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    GameObject FindNearestIncomingBullet()
    {
        GameObject[] bullets = GameObject.FindGameObjectsWithTag("Enemy Bullet");
        GameObject   nearest  = null;
        float        minDist  = dodgeTriggerRadius;

        foreach (GameObject b in bullets)
        {
            if (b == null) continue;
            float d = Vector2.Distance(rb.position, b.transform.position);
            if (d >= minDist) continue;

            // Only dodge if the bullet is actually heading toward us
            Rigidbody2D bRb = b.GetComponent<Rigidbody2D>();
            if (bRb != null && bRb.velocity.sqrMagnitude > 0.01f)
            {
                Vector2 toUs = rb.position - (Vector2)b.transform.position;
                if (Vector2.Dot(bRb.velocity.normalized, toUs.normalized) > 0.3f)
                {
                    minDist = d;
                    nearest  = b;
                }
            }
        }
        return nearest;
    }
}
