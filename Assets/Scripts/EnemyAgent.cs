using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

/// <summary>
/// ML-Agent wrapper for the test enemy. This is a separate ML-Agent implementation
/// and does NOT modify existing enemy AI scripts. It controls Rigidbody2D velocity
/// via two continuous actions (horizontal, vertical).
/// 
/// Notes:
/// - Rigidbody2D must be attached to the same GameObject.
/// - Assign the player's Transform in the Inspector.
/// - The agent will look for existing health components (TestEnemyHealthScript or EnemyHealthScript)
///   and the player's PlayerLivesScript to obtain normalized HP. If none are found, the agent
///   falls back to its own health fields.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAgent : Agent, IDifficultyTunable
{
    [Header("References")]
    [Tooltip("Player Transform (assign in Inspector)")]
    public Transform player;

    [Header("Movement")]
    [Tooltip("Maximum movement speed applied to Rigidbody2D velocity")]
    public float moveSpeed = 4.5f;
    private float baseMoveSpeed; // Tracks inspector moveSpeed for DDA scaling

    [Header("Movement Training")]
    [Tooltip("Distance where the enemy is too close to the player")]
    public float idealRangeMin = 5f;
    [Tooltip("Distance where the enemy is too far from the player")]
    public float idealRangeMax = 8f;
    [Tooltip("Arena minimum corner used for boundary observations and rewards")]
    public Vector2 arenaMin = new Vector2(-8f, -4.5f);
    [Tooltip("Arena maximum corner used for boundary observations and rewards")]
    public Vector2 arenaMax = new Vector2(8f, 4.5f);
    [Tooltip("Distance from arena edge that starts producing boundary penalties")]
    public float boundaryDangerDistance = 1.2f;
    [Tooltip("Observation radius used to normalize nearest player bullet distance")]
    public float bulletObservationRadius = 10f;

    [Header("Threat Tracking (v7)")]
    [Tooltip("Maximum relevant time-to-impact in seconds for normalization")]
    public float maxRelevantTime = 2f;
    [Tooltip("Number of bullet threats to track in observations")]
    public int threatCount = 3;
    [Tooltip("Radius around projected bullet path that triggers danger penalty")]
    public float dangerRadius = 1.5f;
    [Tooltip("Lookahead time (seconds) for projecting bullet paths")]
    public float predictionWindow = 0.3f;
    [Tooltip("Per-bullet penalty strength when inside projected danger zone")]
    public float dangerPenalty = 0.008f;
    [Tooltip("Maximum total danger penalty applied per step")]
    public float maxDangerPenaltyPerStep = 0.03f;

    [Header("Threat Diagnostics")]
    [Tooltip("Enable to print each tracked threat's distance and TTI every FixedUpdate. " +
             "Use this to check if dangerous bullets were visible to the agent before impact. " +
             "Disable before retraining.")]
    public bool logThreatDiagnostics = false;

    [Header("Health (fallback if no health component found)")]
    public float maxHealth = 10f;
    public float currentHealth = 10f;

    [Header("Rewards")]
    [Tooltip("Small positive reward per decision step for staying alive")]
    public float survivalReward = 0.01f;
    [Tooltip("Penalty multiplier applied when taking damage (per HP lost)")]
    public float damagePenalty = 0.02f;
    [Tooltip("Reward given when the agent's projectiles hit the player")]
    public float hitReward = 0.3f;
    [Tooltip("Reward for winning (killing player)")]
    public float winReward = 1.0f;
    [Tooltip("Penalty for dying")]
    public float deathPenalty = -0.3f;

    [Header("Movement Reward Shaping")]
    [Tooltip("Reward for staying in the preferred firing/kiting range")]
    public float idealRangeReward = 0.01f;
    [Tooltip("Penalty for being too close to the player")]
    public float tooClosePenalty = 0.015f;
    [Tooltip("Penalty for being too far from the player")]
    public float tooFarPenalty = 0.005f;
    [Tooltip("Reward for moving laterally around the player while in range")]
    public float lateralMovementReward = 0.02f;
    [Tooltip("Penalty for producing almost no movement")]
    public float idlePenalty = 0.015f;
    [Tooltip("Reward for moving away from an incoming player bullet")]
    public float dodgeReward = 0.04f;
    [Tooltip("Reward given when a bullet passes very close to the agent without hitting")]
    public float nearMissReward = 0.05f;
    [Tooltip("Radius within which a bullet counts as a near miss/graze")]
    public float nearMissRadius = 1.2f;
    [Tooltip("Penalty for staying near arena bounds")]
    public float boundaryPenalty = 0.005f;
    [Tooltip("Penalty for abrupt movement direction changes")]
    public float directionChangePenalty = 0.001f;

    // Tracker for bullets that have already triggered a near-miss reward in this episode
    private HashSet<int> grazedBullets = new HashSet<int>();
    private float episodeDamagePenaltyAccum = 0f;
    private const float MAX_DAMAGE_PENALTY = 0.5f;

    [HideInInspector]
    public float currentSpeedScalar = 1f; // Feeds into observation vector

    // Cached components
    private Rigidbody2D rb;
    private PlayerLivesScript playerHealthScript;
    private Rigidbody2D playerRb;
    private TestEnemyHealthScript testEnemyHealth;
    private EnemyHealthScript enemyHealth;

    /// <summary>
    /// Compact representation of a bullet threat for observation and sorting.
    /// </summary>
    private struct BulletThreat
    {
        public Vector2 relativePosition;
        public Vector2 velocityDirection;
        public float normalizedTimeToImpact;
    }
    // BUG-05 fix: cache manager reference once in Initialize() instead of FindObjectOfType
    // being called on every OnEpisodeBegin (potentially thousands of times per training run).
    private RLTrainingManager _trainingManager;
    // BUG-06 fix: cache the bullet array once per step so CollectObservations,
    // ApplyMovementRewards, and the near-miss loop share a single FindGameObjectsWithTag call.
    private GameObject[] _cachedPlayerBullets = new GameObject[0];
    // BUG-07 fix: store the actual episode time limit from the manager so observations
    // remain correct if the inspector value is changed between runs.
    private float _episodeTimeLimit = 60f;

    // track whether the episode has already ended
    private bool episodeEnded = false;
    private float episodeStartTime;
    private Vector2 previousMove;

    // Called once when the Agent is first initialized
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        baseMoveSpeed = moveSpeed;

        // BUG-05 fix: cache once at initialization
        _trainingManager = FindObjectOfType<RLTrainingManager>();
        // BUG-07 fix: read actual limit from manager if available
        _episodeTimeLimit = _trainingManager != null ? _trainingManager.episodeTimeLimit : 60f;

        if (DanmakuDDAController.Instance != null)
        {
            DanmakuDDAController.Instance.RegisterTunable(this);
        }

        // Make the enemy kinematic so it cannot physically push the player's Rigidbody2D.
        // The agent drives movement via MovePosition; dynamic physics forces are unwanted here.
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        ConfigureTrainingComponents();

        // Try to find health scripts (prefer test-specific)
        testEnemyHealth = GetComponent<TestEnemyHealthScript>();
        enemyHealth = GetComponent<EnemyHealthScript>();

        if (player != null)
        {
            playerHealthScript = player.GetComponent<PlayerLivesScript>();
            playerRb = player.GetComponent<Rigidbody2D>();

            // Disable physics collision between enemy body and player body so the
            // enemy can never push the player. Bullet colliders are on separate
            // GameObjects so they are unaffected — damage detection still works.
            Collider2D enemyCollider = GetComponent<Collider2D>();
            Collider2D playerCollider = player.GetComponent<Collider2D>();
            if (enemyCollider != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(enemyCollider, playerCollider, true);
            }
        }

        // Initialize health from whichever source exists
        if (testEnemyHealth != null)
        {
            maxHealth = testEnemyHealth.maxHealth;
            currentHealth = testEnemyHealth.currentHealth;
        }
        else if (enemyHealth != null)
        {
            maxHealth = enemyHealth.maxHealth;
            currentHealth = enemyHealth.currentHealth;
        }

    }

    private void ConfigureTrainingComponents()
    {
        DecisionRequester requester = GetComponent<DecisionRequester>();
        if (requester == null)
            requester = gameObject.AddComponent<DecisionRequester>();

        requester.DecisionPeriod = 5;
        requester.DecisionStep = 0;
        requester.TakeActionsBetweenDecisions = true;
    }
    // Called at the beginning of each episode (reset expected to be handled by environment manager)
    public override void OnEpisodeBegin()
    {
        // Refresh references in case environment reset instantiated/changed objects
        if (player != null && playerHealthScript == null)
            playerHealthScript = player.GetComponent<PlayerLivesScript>();

        // Re-sync health values from components (RLTrainingManager should reset underlying components)
        if (testEnemyHealth != null)
        {
            currentHealth = testEnemyHealth.currentHealth;
            maxHealth = testEnemyHealth.maxHealth;
        }
        else if (enemyHealth != null)
        {
            currentHealth = enemyHealth.currentHealth;
            maxHealth = enemyHealth.maxHealth;
        }

        episodeEnded = false;
        episodeStartTime = Time.time;
        previousMove = Vector2.zero;
        grazedBullets.Clear();
        episodeDamagePenaltyAccum = 0f;

        // If training manager is active, randomize training difficulty to train across speed spectrum
        // BUG-05 fix: use the cached reference instead of FindObjectOfType every episode.
        if (_trainingManager != null)
        {
            currentSpeedScalar = Random.Range(0.2f, 1.0f);
            moveSpeed = baseMoveSpeed * currentSpeedScalar;
        }
        else
        {
            currentSpeedScalar = 1f;
        }

        // Ensure Rigidbody is not carrying over momentum
        // (kinematic bodies don't use velocity — just zero it for safety)
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    void FixedUpdate()
    {
        if (logThreatDiagnostics)
            LogThreatDiagnostics();
    }

    // Observations required by the policy
    public override void CollectObservations(VectorSensor sensor)
    {
        if (sensor == null) return;

        Vector2 enemyPos = transform.position;
        Vector2 playerPos = player != null ? (Vector2)player.position : Vector2.zero;

        // 1. Normalized enemy position (2)
        sensor.AddObservation(NormalizePosition(enemyPos));

        // 2. Normalized player position (2)
        sensor.AddObservation(NormalizePosition(playerPos));

        // 3. Relative player offset x/y (2)
        Vector2 offset = playerPos - enemyPos;
        sensor.AddObservation(offset);

        // 4. Normalized distance to player (1)
        float distance = offset.magnitude;
        float maxPossibleDist = Vector2.Distance(arenaMin, arenaMax);
        sensor.AddObservation(maxPossibleDist > 0f ? distance / maxPossibleDist : 0f);

        // 5. Direction to player x/y (2)
        sensor.AddObservation(distance > 0f ? offset.normalized : Vector2.zero);

        // 6. Enemy velocity x/y (2)
        sensor.AddObservation(rb != null ? rb.velocity : Vector2.zero);

        // 7. Player velocity x/y (2)
        sensor.AddObservation(playerRb != null ? playerRb.velocity : Vector2.zero);

        // 8. Normalized enemy HP (1)
        sensor.AddObservation(GetEnemyHealthNormalized());

        // 9. Normalized player HP (1)
        sensor.AddObservation(GetPlayerHealthNormalized());

        // 10. DDA current difficulty (1)
        // 11. DDA current pressure (1)
        if (DanmakuDDAController.Instance != null)
        {
            sensor.AddObservation(DanmakuDDAController.Instance.currentDifficulty);
            sensor.AddObservation(DanmakuDDAController.Instance.currentPressure);
        }
        else
        {
            sensor.AddObservation(0.5f);
            sensor.AddObservation(0f);
        }

        // 12–26. Top-3 bullet threats sorted by time-to-impact (5 per threat × 3 = 15)
        var threats = GetTopThreats(enemyPos, threatCount);
        for (int i = 0; i < threatCount; i++)
        {
            if (i < threats.Count)
            {
                sensor.AddObservation(threats[i].relativePosition);       // 2
                sensor.AddObservation(threats[i].velocityDirection);       // 2
                sensor.AddObservation(threats[i].normalizedTimeToImpact);  // 1
            }
            else
            {
                // Pad with zeros when fewer bullets exist than slots
                sensor.AddObservation(Vector2.zero); // 2
                sensor.AddObservation(Vector2.zero); // 2
                sensor.AddObservation(1f);            // 1 (max TTI = no threat)
            }
        }

        // 27. Boundary distances: left, right, bottom, top (4)
        Vector4 boundaryDistances = GetNormalizedBoundaryDistances(enemyPos);
        sensor.AddObservation(boundaryDistances.x); // left
        sensor.AddObservation(boundaryDistances.y); // right
        sensor.AddObservation(boundaryDistances.z); // bottom
        sensor.AddObservation(boundaryDistances.w); // top

        // 28. Episode time progress 0-1 (1)
        float episodeElapsed = Time.time - episodeStartTime;
        // BUG-07 fix: use the actual limit from the manager instead of a hard-coded 60f.
        sensor.AddObservation(Mathf.Clamp01(episodeElapsed / _episodeTimeLimit));

        // 29. Current speed scalar (1)
        sensor.AddObservation(currentSpeedScalar);

        // Total: 2+2+2+1+2+2+2+1+1+2+15+4+1+1 = 38
    }

    // Called when the model outputs an action. Two continuous actions expected.
    public override void OnActionReceived(ActionBuffers actions)
    {
        // BUG-06 fix: refresh the bullet cache once here so CollectObservations,
        // ApplyMovementRewards, and the near-miss loop all share this single allocation.
        _cachedPlayerBullets = GameObject.FindGameObjectsWithTag("Player Bullet");

        // Continuous actions: [0]=horizontal, [1]=vertical (range [-1,1])
        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];

        // Safety for invalid model output
        if (float.IsNaN(moveX) || float.IsNaN(moveY) || float.IsInfinity(moveX) || float.IsInfinity(moveY))
        {
            moveX = 0f;
            moveY = 0f;
        }

        moveX = Mathf.Clamp(moveX, -1f, 1f);
        moveY = Mathf.Clamp(moveY, -1f, 1f);

        Vector2 move = new Vector2(moveX, moveY);
        if (move.sqrMagnitude > 1f) move = move.normalized;

        // Move via MovePosition — correct API for a kinematic Rigidbody2D.
        // This avoids applying physics forces to the player on contact.
        if (rb != null)
        {
            rb.MovePosition(rb.position + move * moveSpeed * Time.fixedDeltaTime);
        }

        // Clamp enemy position to arena bounds to prevent drifting off-screen (with padding to stay fully on-screen)
        Vector3 clampedPosition = transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, arenaMin.x + 0.8f, arenaMax.x - 0.8f);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, arenaMin.y + 0.8f, arenaMax.y - 0.8f);
        transform.position = clampedPosition;

        // Small positive reward each step to encourage survival/active behavior
        AddReward(survivalReward);

        // Progressive survival bonus (surviving longer is rewarded)
        float episodeElapsed = Time.time - episodeStartTime;
        // BUG-07 fix: use the actual limit from the manager.
        float survivalProgress = Mathf.Clamp01(episodeElapsed / _episodeTimeLimit);
        AddReward(0.005f * survivalProgress);

        // Add +0.002 per step when player HP ratio is between 0.3-0.7
        float playerHPRatio = GetPlayerHealthNormalized();
        if (playerHPRatio >= 0.3f && playerHPRatio <= 0.7f)
        {
            AddReward(0.002f);
        }

        // Apply movement rewards
        ApplyMovementRewards(move);

        if (playerHealthScript != null && playerHealthScript.currentHealth <= 0f)
        {
            AddReward(winReward);
            EndEpisode();
        }
    }

    // Heuristic for testing — maps to player input using IJKL keys to avoid WASD conflict
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.J)) horizontal = -1f;
        if (Input.GetKey(KeyCode.L)) horizontal = 1f;
        if (Input.GetKey(KeyCode.I)) vertical = 1f;
        if (Input.GetKey(KeyCode.K)) vertical = -1f;

        continuous[0] = horizontal;
        continuous[1] = vertical;
    }

    private Vector2 NormalizePosition(Vector2 position)
    {
        float rangeX = arenaMax.x - arenaMin.x;
        float rangeY = arenaMax.y - arenaMin.y;
        float x = rangeX > 0f ? (position.x - arenaMin.x) / rangeX : 0.5f;
        float y = rangeY > 0f ? (position.y - arenaMin.y) / rangeY : 0.5f;
        return new Vector2(x, y);
    }

    private Vector4 GetNormalizedBoundaryDistances(Vector2 position)
    {
        float left = Mathf.Clamp01((position.x - arenaMin.x) / boundaryDangerDistance);
        float right = Mathf.Clamp01((arenaMax.x - position.x) / boundaryDangerDistance);
        float bottom = Mathf.Clamp01((position.y - arenaMin.y) / boundaryDangerDistance);
        float top = Mathf.Clamp01((arenaMax.y - position.y) / boundaryDangerDistance);
        return new Vector4(left, right, bottom, top);
    }

    private GameObject GetNearestPlayerBullet(Vector2 enemyPosition)
    {
        // BUG-06 fix: iterate the cached array filled at the start of OnActionReceived.
        GameObject nearest = null;
        float minDistance = float.MaxValue;
        foreach (GameObject bullet in _cachedPlayerBullets)
        {
            if (bullet == null) continue;
            float dist = Vector2.Distance(enemyPosition, bullet.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = bullet;
            }
        }
        return nearest;
    }

    /// <summary>
    /// Prints one line per tracked threat to the Unity Console so you can verify
    /// whether fast-approaching bullets were already visible to the agent.
    /// Toggle via the logThreatDiagnostics Inspector checkbox.
    /// </summary>
    private void LogThreatDiagnostics()
    {
        // Re-use the bullet cache that OnActionReceived already filled this frame.
        // If called before the first OnActionReceived, pull a fresh list.
        GameObject[] bullets = _cachedPlayerBullets.Length > 0
            ? _cachedPlayerBullets
            : GameObject.FindGameObjectsWithTag("Player Bullet");

        Vector2 enemyPos = transform.position;
        var threats = GetTopThreats(enemyPos, threatCount);

        if (threats.Count == 0)
        {
            Debug.Log("[ThreatDiag] No threats in top-list this frame.");
            return;
        }

        // Also compute raw distances for every bullet so we can cross-check
        // which ones are close but NOT in the tracked list (perception gap).
        for (int i = 0; i < threats.Count; i++)
        {
            float dist   = threats[i].relativePosition.magnitude;
            float ttiSec = threats[i].normalizedTimeToImpact * maxRelevantTime;
            Debug.Log($"[ThreatDiag] Threat[{i}] dist={dist:F2}u  TTI={ttiSec:F3}s  " +
                      $"normTTI={threats[i].normalizedTimeToImpact:F3}  " +
                      $"velDir={threats[i].velocityDirection}");
        }

        // Log any bullets that are CLOSER than the nearest tracked threat
        // but did NOT make it into the top-3 list (signals a perception gap).
        float nearestTrackedDist = threats[0].relativePosition.magnitude;
        foreach (GameObject b in bullets)
        {
            if (b == null) continue;
            float d = Vector2.Distance(enemyPos, b.transform.position);
            if (d < nearestTrackedDist)
            {
                Debug.LogWarning($"[ThreatDiag] PERCEPTION GAP — bullet at dist={d:F2}u is closer " +
                                 $"than nearest tracked threat ({nearestTrackedDist:F2}u) " +
                                 $"but NOT in the top-{threatCount} list. " +
                                 $"Check bulletObservationRadius or moving-toward filter.");
            }
        }
    }

    /// <summary>
    /// Returns the top N bullet threats sorted by ascending time-to-impact.
    /// </summary>
    private List<BulletThreat> GetTopThreats(Vector2 enemyPosition, int count)
    {
        var threats = new List<BulletThreat>();

        foreach (GameObject bullet in _cachedPlayerBullets)
        {
            if (bullet == null) continue;

            Vector2 bulletPos = bullet.transform.position;
            Vector2 relPos = bulletPos - enemyPosition;
            float dist = relPos.magnitude;

            // Skip bullets outside observation radius
            if (dist > bulletObservationRadius) continue;

            Vector2 bulletVel = GetBulletVelocity(bullet);
            float bulletSpeed = bulletVel.magnitude;
            Vector2 velDir = bulletSpeed > 0.1f ? bulletVel / bulletSpeed : Vector2.zero;

            // Only consider bullets moving toward the agent
            if (bulletSpeed > 0.1f)
            {
                Vector2 bulletToEnemy = -relPos;
                if (Vector2.Dot(velDir, bulletToEnemy.normalized) <= 0f)
                    continue; // moving away — not a threat
            }
            else
            {
                continue; // stationary — not a threat
            }

            // Time-to-impact: distance / closing speed
            float closingSpeed = Mathf.Max(bulletSpeed, 0.01f);
            float tti = dist / closingSpeed;
            float normalizedTTI = Mathf.Clamp01(tti / maxRelevantTime);

            threats.Add(new BulletThreat
            {
                relativePosition = relPos,
                velocityDirection = velDir,
                normalizedTimeToImpact = normalizedTTI
            });
        }

        // Sort by ascending TTI (most urgent first)
        threats.Sort((a, b) => a.normalizedTimeToImpact.CompareTo(b.normalizedTimeToImpact));

        // Return only the top N
        if (threats.Count > count)
            threats.RemoveRange(count, threats.Count - count);

        return threats;
    }

    /// <summary>
    /// Returns the shortest distance from a point to a line segment.
    /// </summary>
    private float DistanceToLineSegment(Vector2 point, Vector2 segStart, Vector2 segEnd)
    {
        Vector2 seg = segEnd - segStart;
        float segLenSq = seg.sqrMagnitude;
        if (segLenSq < 0.0001f)
            return Vector2.Distance(point, segStart);

        // Project point onto the line, clamped to [0,1]
        float t = Mathf.Clamp01(Vector2.Dot(point - segStart, seg) / segLenSq);
        Vector2 projection = segStart + t * seg;
        return Vector2.Distance(point, projection);
    }

    private Vector2 GetBulletVelocity(GameObject bullet)
    {
        if (bullet == null) return Vector2.zero;
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
            return bulletRb.velocity;
        return Vector2.zero;
    }

    private void ApplyMovementRewards(Vector2 move)
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // 1. Shaped ideal range reward / penalty (continuous gradient to avoid zero-gradient boundaries)
        float idealMid = (idealRangeMin + idealRangeMax) / 2f;
        float rangeReward;
        if (distance >= idealRangeMin && distance <= idealRangeMax)
        {
            // Full reward at midpoint, tapers to 0 at edges
            float rangeWidth = idealMid - idealRangeMin;
            float normalized = 1f - (Mathf.Abs(distance - idealMid) / rangeWidth);
            rangeReward = idealRangeReward * normalized;
        }
        else
        {
            // Penalty that grows with distance from ideal zone
            float overshoot = distance < idealRangeMin ? (idealRangeMin - distance) : (distance - idealRangeMax);
            rangeReward = -tooClosePenalty * overshoot;
        }
        AddReward(rangeReward);

        // 2. Lateral movement reward: perpendicular to player direction
        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        Vector2 perpendicular = new Vector2(-toPlayer.y, toPlayer.x);
        float lateralSpeed = Mathf.Abs(Vector2.Dot(move, perpendicular));
        if (distance >= idealRangeMin && distance <= idealRangeMax)
        {
            AddReward(lateralSpeed * lateralMovementReward);
        }

        // 3. Idle penalty
        if (move.sqrMagnitude < 0.01f)
        {
            AddReward(-idlePenalty);
        }

        // 4. Direction change penalty
        if (previousMove.sqrMagnitude > 0.01f && move.sqrMagnitude > 0.01f)
        {
            float change = 1f - Vector2.Dot(previousMove.normalized, move.normalized);
            if (change > 0.5f)
            {
                AddReward(-directionChangePenalty * (change / 2f));
            }
        }

        // 5. Boundary penalty
        Vector2 pos = transform.position;
        float distLeft = pos.x - arenaMin.x;
        float distRight = arenaMax.x - pos.x;
        float distBottom = pos.y - arenaMin.y;
        float distTop = arenaMax.y - pos.y;

        if (distLeft < boundaryDangerDistance)
            AddReward(-boundaryPenalty * (1f - (distLeft / boundaryDangerDistance)));
        if (distRight < boundaryDangerDistance)
            AddReward(-boundaryPenalty * (1f - (distRight / boundaryDangerDistance)));
        if (distBottom < boundaryDangerDistance)
            AddReward(-boundaryPenalty * (1f - (distBottom / boundaryDangerDistance)));
        if (distTop < boundaryDangerDistance)
            AddReward(-boundaryPenalty * (1f - (distTop / boundaryDangerDistance)));

        // 6. Dodge reward
        GameObject nearestBullet = GetNearestPlayerBullet(pos);
        if (nearestBullet != null)
        {
            Vector2 bulletPos = nearestBullet.transform.position;
            Vector2 bulletVel = GetBulletVelocity(nearestBullet);
            if (bulletVel.sqrMagnitude > 0.01f)
            {
                Vector2 bulletToEnemy = (Vector2)transform.position - bulletPos;
                if (Vector2.Dot(bulletVel.normalized, bulletToEnemy.normalized) > 0f)
                {
                    float dodgeFactor = Vector2.Dot(move.normalized, bulletToEnemy.normalized);
                    if (dodgeFactor > 0f && move.sqrMagnitude > 0.01f)
                    {
                        AddReward(dodgeReward * dodgeFactor);
                    }
                }
            }
        }

        // 7. Near Miss (Graze) reward
        // BUG-06 fix: reuse the cached array instead of a third FindGameObjectsWithTag call.
        foreach (GameObject bullet in _cachedPlayerBullets)
        {
            if (bullet == null) continue;
            int bulletId = bullet.GetInstanceID();
            if (grazedBullets.Contains(bulletId)) continue;

            float distToBullet = Vector2.Distance(pos, bullet.transform.position);
            if (distToBullet > 0.4f && distToBullet <= nearMissRadius)
            {
                grazedBullets.Add(bulletId);
                AddReward(nearMissReward);
                Debug.Log($"[Graze] reward +{nearMissReward:F2} | bullet {bulletId} | dist {distToBullet:F3}");
            }
        }

        // 8. Danger-zone penalty: continuous penalty for being inside projected bullet paths
        float totalDangerPenalty = 0f;
        foreach (GameObject bullet in _cachedPlayerBullets)
        {
            if (bullet == null) continue;
            Vector2 bulletPos = bullet.transform.position;
            Vector2 bulletVelDanger = GetBulletVelocity(bullet);
            if (bulletVelDanger.sqrMagnitude < 0.01f) continue;

            // Only consider bullets within observation radius
            if (Vector2.Distance(pos, bulletPos) > bulletObservationRadius) continue;

            Vector2 futurePos = bulletPos + bulletVelDanger * predictionWindow;
            float distToPath = DistanceToLineSegment(pos, bulletPos, futurePos);

            if (distToPath < dangerRadius)
            {
                float penaltyAmount = dangerPenalty * (1f - distToPath / dangerRadius);
                totalDangerPenalty += penaltyAmount;
            }
        }
        // Cap total danger penalty per step
        totalDangerPenalty = Mathf.Min(totalDangerPenalty, maxDangerPenaltyPerStep);
        if (totalDangerPenalty > 0f)
        {
            AddReward(-totalDangerPenalty);
        }

        previousMove = move;
    }

    // Public API: call this from other game code when the agent takes damage
    // Will try to apply damage to underlying health component if present, otherwise update internal health
    public void TakeDamage(float damage)
    {
        if (damage <= 0f) return;

        if (testEnemyHealth != null)
        {
            testEnemyHealth.TakeDamage(damage);
            currentHealth = testEnemyHealth.currentHealth;
        }
        else if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            currentHealth = enemyHealth.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - damage);
        }

        // Apply immediate penalty (capped per episode to avoid destroying the learning signal)
        float penalty = Mathf.Min(damagePenalty * damage, MAX_DAMAGE_PENALTY - episodeDamagePenaltyAccum);
        episodeDamagePenaltyAccum += penalty;
        AddReward(-penalty);

        if (currentHealth <= 0f)
        {
            AddReward(deathPenalty);
            EndEpisode();
        }
    }

    /// <summary>
    /// Shadow EndEpisode to ensure it is only triggered once per episode.
    /// </summary>
    public new void EndEpisode()
    {
        if (!episodeEnded)
        {
            episodeEnded = true;

            // Removed the short-episode penalty: it was redundant with the
            // death penalty and created a double-punishment that made the agent
            // learn to avoid all action.

            base.EndEpisode();
        }
    }

    // Public API: call this when the agent's projectile hits the player to give a reward
    public void RewardForHit(float multiplier = 1f)
    {
        AddReward(hitReward * multiplier);
    }

    // Utility: read current health from available sources
    private float ReadEnemyHealth()
    {
        if (testEnemyHealth != null) return testEnemyHealth.currentHealth;
        if (enemyHealth != null) return enemyHealth.currentHealth;
        return currentHealth;
    }

    // Utility: normalized enemy hp 0..1
    private float GetEnemyHealthNormalized()
    {
        float mh = maxHealth;
        if (testEnemyHealth != null) mh = testEnemyHealth.maxHealth;
        else if (enemyHealth != null) mh = enemyHealth.maxHealth;

        float ch = ReadEnemyHealth();
        return mh > 0f ? Mathf.Clamp01(ch / mh) : 0f;
    }

    // Utility: normalized player hp 0..1
    private float GetPlayerHealthNormalized()
    {
        if (player == null || playerHealthScript == null) return 0f;
        return playerHealthScript.maxHealth > 0f ? Mathf.Clamp01(playerHealthScript.currentHealth / playerHealthScript.maxHealth) : 0f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        // ensure Rigidbody is stopped when disabled
        if (rb != null)
            rb.velocity = Vector2.zero;
    }

    public void ApplyDifficulty(DifficultyProfile profile)
    {
        currentSpeedScalar = profile.enemySpeedMultiplier;
        moveSpeed = baseMoveSpeed * currentSpeedScalar;
    }
}

