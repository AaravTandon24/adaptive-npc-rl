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
public class EnemyAgent : Agent
{
    [Header("References")]
    [Tooltip("Player Transform (assign in Inspector)")]
    public Transform player;

    [Header("Movement")]
    [Tooltip("Maximum movement speed applied to Rigidbody2D velocity")]
    public float moveSpeed = 3f;

    [Header("Health (fallback if no health component found)")]
    public float maxHealth = 10f;
    public float currentHealth = 10f;

    [Header("Rewards")]
    [Tooltip("Small positive reward per decision step for staying alive")]
    public float survivalReward = 0.001f;
    [Tooltip("Penalty multiplier applied when taking damage")]
    public float damagePenalty = 0.1f;
    [Tooltip("Reward given when the agent's projectiles hit the player")]
    public float hitReward = 0.5f;
    [Tooltip("Reward for winning (killing player)")]
    public float winReward = 1f;
    [Tooltip("Penalty for dying")]
    public float deathPenalty = -1f;

    // Cached components
    private Rigidbody2D rb;
    private PlayerLivesScript playerHealthScript;
    private TestEnemyHealthScript testEnemyHealth;
    private EnemyHealthScript enemyHealth;

    // track previous health to detect damage taken
    private float previousHealth;

    // Called once when the Agent is first initialized
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureTrainingComponents();

        // Try to find health scripts (prefer test-specific)
        testEnemyHealth = GetComponent<TestEnemyHealthScript>();
        enemyHealth = GetComponent<EnemyHealthScript>();

        if (player != null)
            playerHealthScript = player.GetComponent<PlayerLivesScript>();

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

        previousHealth = currentHealth;
    }

    private void ConfigureTrainingComponents()
    {
        BehaviorParameters behaviorParameters = GetComponent<BehaviorParameters>();
        if (behaviorParameters != null)
        {
            behaviorParameters.BehaviorName = "EnemyAgent";
            behaviorParameters.BehaviorType = BehaviorType.Default;
            behaviorParameters.BrainParameters.VectorObservationSize = 9;
            behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
            behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
            behaviorParameters.BrainParameters.VectorActionDescriptions = new[] { "Move X", "Move Y" };
        }

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

        previousHealth = currentHealth;

        // Ensure Rigidbody is not carrying over momentum
        if (rb != null)
            rb.velocity = Vector2.zero;
    }

    // Observations required by the policy
    public override void CollectObservations(VectorSensor sensor)
    {
        // Basic positions (unscaled)
        sensor.AddObservation(transform.position.x);
        sensor.AddObservation(transform.position.y);

        // Player position (guarded)
        if (player != null)
        {
            sensor.AddObservation(player.position.x);
            sensor.AddObservation(player.position.y);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Distance to player (scalar)
        float distance = (player != null) ? Vector2.Distance(transform.position, player.position) : 0f;
        sensor.AddObservation(distance);

        // Normalized enemy HP
        float enemyHPnorm = GetEnemyHealthNormalized();
        sensor.AddObservation(enemyHPnorm);

        // Normalized player HP
        float playerHPnorm = GetPlayerHealthNormalized();
        sensor.AddObservation(playerHPnorm);

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
    }

    // Called when the model outputs an action. Two continuous actions expected.
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Continuous actions: [0]=horizontal, [1]=vertical (range [-1,1])
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveY = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector2 move = new Vector2(moveX, moveY);
        if (move.sqrMagnitude > 1f) move = move.normalized;

        // Apply velocity to Rigidbody2D
        if (rb != null)
        {
            rb.velocity = move * moveSpeed;
        }

        // Small positive reward each step to encourage survival/active behavior
        AddReward(survivalReward);

        // Detect damage taken this step and apply penalty
        float current = ReadEnemyHealth();
        if (current < previousHealth)
        {
            float dmgTaken = previousHealth - current;
            AddReward(-damagePenalty * dmgTaken);
        }
        previousHealth = current;

        // Check terminal conditions locally as safety (environment manager may also trigger)
        if (current <= 0f)
        {
            AddReward(deathPenalty);
            EndEpisode();
        }

        if (playerHealthScript != null && playerHealthScript.currentHealth <= 0f)
        {
            AddReward(winReward);
            EndEpisode();
        }
    }

    // Heuristic for testing — maps to player input (WASD / arrows)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxisRaw("Horizontal"); // -1..1
        continuous[1] = Input.GetAxisRaw("Vertical");   // -1..1
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

        // Apply immediate penalty
        AddReward(-damagePenalty * damage);

        if (currentHealth <= 0f)
        {
            AddReward(deathPenalty);
            EndEpisode();
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
}
