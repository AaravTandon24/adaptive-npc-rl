using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public enum TrainingCondition
{
    StaticEasy,
    StaticHard,
    RuleBasedDDA
}

/// <summary>
/// GameManager specifically designed for Reinforcement Learning training environment.
/// Manages episodes between Player and Enemy without scene reloads.
/// </summary>
public class RLTrainingManager : MonoBehaviour
{
    [Header("Baseline Condition")]
    public TrainingCondition trainingCondition = TrainingCondition.RuleBasedDDA;

    [Header("Entity References")]
    [Tooltip("Reference to the Player GameObject")]
    public GameObject player;
    
    [Tooltip("Reference to the Enemy GameObject")]
    public GameObject enemy;

    [Header("Episode Settings")]
    [Tooltip("Maximum duration of an episode in seconds")]
    public float episodeTimeLimit = 60f;

    [Tooltip("Maximum number of episodes to run (0 for infinite)")]
    public int maxEpisodes = 500;

    [Header("Episode Tracking (Read-Only)")]
    [Tooltip("Current time elapsed in the episode")]
    public float currentEpisodeTime = 0f;
    
    [Tooltip("Total number of episodes completed")]
    public int episodeCount = 0;

    [Header("Performance Metrics")]
    [Tooltip("Total damage dealt by player this episode")]
    public float playerDamageDealt = 0f;
    
    [Tooltip("Total damage dealt by enemy this episode")]
    public float enemyDamageDealt = 0f;
    
    [Tooltip("Time the player survived this episode")]
    public float timeSurvived = 0f;

    [Header("Episode Logging")]
    public bool writeEpisodeCsv = true;
    public string logDirectoryName = "RLTrainingLogs";
    public string csvFileName = "enemy_agent_episodes.csv";

    [Header("Fuzzy Tier Classifier")]
    [Tooltip("Attach the FuzzyTierClassifier component here.")]
    [SerializeField] private FuzzyTierClassifier _tierClassifier;

    // Private cached references
    private Vector3 playerStartPosition;
    private Vector3 enemyStartPosition;
    private PlayerLivesScript playerHealthScript;
    private PlayerMovement playerMovement;
    private TestEnemyHealthScript enemyHealthScript;
    private EnemyAgent enemyAgent;
    private TestEnemyScript testEnemyScript;
    private PlayerPerformanceTelemetry playerTelemetry;
    private GameOverScript gameOverScript;
    private bool episodeActive = false;
    private string lastEpisodeOutcome = "unknown";

    // New telemetry counters for enemy
    private int enemyShotsFired = 0;
    private int enemyShotsHit = 0;

    // Snapshot of active difficulty parameters at episode START
    private float start_fireRate = 0f;
    private float start_bulletSpeed = 0f;
    private float start_spreadAngle = 0f;
    private float start_enemyMoveSpeed = 0f;

    // Pressure captured at episode end, before bullets are destroyed
    private float capturedPressure = 0f;
    private float capturedDifficulty = 0f;
    private int capturedActiveBullets = 0;
    private BulletPressureAnalyzer pressureAnalyzer;

    // Running pressure average sampled throughout the episode
    private float runningPressureSum = 0f;
    private int runningPressureSamples = 0;

    // Rolling outcomes for last N episodes for win rate
    private readonly Queue<int> recentOutcomes = new Queue<int>();
    private readonly Queue<float> recentSurvivalTimes = new Queue<float>();
    private const int RollingWindowSize = 10;

    void Start()
    {
        csvFileName = GetLogFileName();
        InitializeEpisode();
    }

    void Update()
    {
        if (!episodeActive) return;

        // Sample pressure every frame to build a meaningful per-episode average.
        // Sampling at episode END misses all real threat moments.
        if (pressureAnalyzer == null)
        {
            pressureAnalyzer = FindObjectOfType<BulletPressureAnalyzer>();
        }

        if (pressureAnalyzer != null)
        {
            runningPressureSum += pressureAnalyzer.GetPressureScore();
            runningPressureSamples++;
        }

        // Update episode timer
        currentEpisodeTime += Time.deltaTime;
        timeSurvived = currentEpisodeTime;

        // Check end conditions
        CheckEpisodeEndConditions();
    }

    /// <summary>
    /// Initialize the first episode and cache necessary references
    /// </summary>
    private void InitializeEpisode()
    {
        // Validate references
        if (player == null || enemy == null)
        {
            Debug.LogError("RLTrainingManager: Player or Enemy reference is missing!");
            return;
        }

        // Cache starting positions
        playerStartPosition = player.transform.position;
        enemyStartPosition = enemy.transform.position;

        // Cache health script references
        playerHealthScript = player.GetComponent<PlayerLivesScript>();
        enemyHealthScript = enemy.GetComponent<TestEnemyHealthScript>();
        enemyAgent = enemy.GetComponent<EnemyAgent>();
        testEnemyScript = enemy.GetComponent<TestEnemyScript>();
        playerTelemetry = player.GetComponent<PlayerPerformanceTelemetry>();
        playerMovement = player.GetComponent<PlayerMovement>();

        // Cache pressure analyzer — works even when DDA component is disabled
        pressureAnalyzer = FindObjectOfType<BulletPressureAnalyzer>();

        // Cache the Game Over UI so we can hide it during episode resets
        gameOverScript = FindObjectOfType<GameOverScript>(true);

        if (playerHealthScript == null)
        {
            Debug.LogError("RLTrainingManager: PlayerLivesScript not found on Player!");
            return;
        }

        // Suppress the Game Over UI and timeScale=0 — we handle resets ourselves
        playerHealthScript.trainingMode = true;

        if (enemyHealthScript == null)
        {
            Debug.LogError("RLTrainingManager: TestEnemyHealthScript not found on Enemy!");
            return;
        }

        // Listen for player death so RLTrainingManager can end the episode.
        playerHealthScript.OnDiedTraining.AddListener(OnPlayerDied);

        // Start first episode
        ResetEpisode();
        episodeActive = true;
    }

    /// <summary>
    /// Called by PlayerLivesScript.OnDied the moment the player's HP hits 0.
    /// Ends the episode before GameOver() can freeze Time.timeScale.
    /// </summary>
    private void OnPlayerDied()
    {
        if (!episodeActive) return;
        Debug.Log("Episode ended: Player defeated");
        lastEpisodeOutcome = "player_defeated";
        EndEpisode();
    }

    /// <summary>
    /// Check if any episode end conditions are met
    /// </summary>
    private void CheckEpisodeEndConditions()
    {
        // Check if enemy was destroyed externally
        if (enemy == null)
        {
            Debug.LogWarning("Enemy was destroyed! Episode ending...");
            lastEpisodeOutcome = "player_defeated";
            EndEpisode();
            return;
        }

        // Condition 1: Player death is now handled via the OnDied event
        // (registered in InitializeEpisode). Polling is kept as a safety
        // fallback only — the event fires before Time.timeScale is frozen.

        // Condition 2: Enemy HP reaches 0
        if (enemyHealthScript.currentHealth <= 0)
        {
            Debug.Log("Episode ended: Enemy defeated");
            lastEpisodeOutcome = "enemy_defeated";
            EndEpisode();
            return;
        }

        // Condition 3: Time limit reached - resets immediately like enemy death
        if (currentEpisodeTime >= episodeTimeLimit)
        {
            Debug.Log("Episode ended: Time limit reached");
            lastEpisodeOutcome = "timeout";
            EndEpisode();
            return;
        }
    }

    /// <summary>
    /// End the current episode and prepare for reset
    /// </summary>
    public void EndEpisode()
    {
        episodeActive = false;
        episodeCount++;

        // Capture average pressure across the episode (sampled every frame).
        // End-of-episode snapshot is useless because bullets are mid-flight away from the action.
        capturedPressure      = runningPressureSamples > 0 ? runningPressureSum / runningPressureSamples : 0f;
        capturedDifficulty    = DanmakuDDAController.Instance != null ? DanmakuDDAController.Instance.currentDifficulty : 0f;
        capturedActiveBullets = GameObject.FindGameObjectsWithTag("Enemy Bullet").Length;

        // Log episode statistics
        LogEpisodeStats();

        if (maxEpisodes > 0 && episodeCount >= maxEpisodes)
        {
            Debug.Log($"RLTrainingManager: Reached max episode limit of {maxEpisodes}. Quitting application.");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
            return;
        }

        if (DanmakuDDAController.Instance != null)
        {
            DanmakuDDAController.Instance.OnEpisodeEnd(episodeCount);
        }

        if (enemyAgent != null)
            enemyAgent.EndEpisode();

        // Reset for next episode
        ResetEpisode();
        episodeActive = true;
    }

    /// <summary>
    /// Reset all episode variables and entity states
    /// </summary>
    public void ResetEpisode()
    {
        ApplyTrainingCondition();

        // Reset timer
        currentEpisodeTime = 0f;
        lastEpisodeOutcome = "unknown";

        // Reset performance metrics
        playerDamageDealt = 0f;
        enemyDamageDealt = 0f;
        timeSurvived = 0f;

        // Reset enemy telemetry counters
        enemyShotsFired = 0;
        enemyShotsHit = 0;

        // Reset player telemetry so shots/hits/damage are per-episode, not cumulative
        if (playerTelemetry != null)
            playerTelemetry.ResetEpisode();

        // Reset pressure accumulator
        runningPressureSum = 0f;
        runningPressureSamples = 0;

        // Destroy all projectiles IMMEDIATELY so none can re-damage
        // the player on the same frame it reactivates.
        DestroyAllProjectiles();

        // Reset player using the clean training-mode API.
        if (player != null)
        {
            // Move to start position first.
            player.transform.position = playerStartPosition;

            // Hide the Game Over UI if somehow visible.
            if (gameOverScript != null)
                gameOverScript.gameObject.SetActive(false);

            // Resets isDead, health, timeScale, re-enables the GameObject, and wakes the Rigidbody2D.
            playerHealthScript.ResetForEpisode();

            // === CRITICAL: Re-enable movement & shooting components ===
            // The OnDied UnityEvent on the Player prefab disables PlayerMovement.
            // After a reset we must explicitly re-enable it or the player can't move.
            // If RLPlayerBot is present and active, we keep PlayerMovement disabled so the bot can move.
            RLPlayerBot playerBot = player.GetComponent<RLPlayerBot>();
            if (playerMovement != null && (playerBot == null || !playerBot.enabled))
                playerMovement.enabled = true;

            Shooting shooting = player.GetComponent<Shooting>();
            if (shooting != null)
                shooting.enabled = true;
        }

        // Reset enemy
        if (enemy != null)
        {
            enemy.transform.position = enemyStartPosition;
            enemyHealthScript.ResetHealth();
            
            if (!enemy.activeInHierarchy)
                enemy.SetActive(true);

            // Snapshot active difficulty parameters at episode START
            if (testEnemyScript != null)
            {
                start_fireRate = testEnemyScript.fireRate;
                start_bulletSpeed = testEnemyScript.bulletSpeed;
                start_spreadAngle = testEnemyScript.spreadAngle;
                start_enemyMoveSpeed = testEnemyScript.movementSpeed;
            }
            else
            {
                // Fallback to DDA controller profile if present
                if (DanmakuDDAController.Instance != null)
                {
                    var p = DanmakuDDAController.Instance.CurrentProfile;
                    start_fireRate = p.fireRateMultiplier;
                    start_bulletSpeed = p.bulletSpeedMultiplier;
                    start_spreadAngle = p.spreadAngleMultiplier;
                    start_enemyMoveSpeed = p.enemySpeedMultiplier;
                }
            }
        }
        else
        {
            Debug.LogError("Enemy is null! Cannot reset episode. Make sure enemy is not being destroyed.");
        }

        Debug.Log($"Episode {episodeCount + 1} started");
    }


    /// <summary>
    /// Destroy all active projectiles in the scene.
    /// Disables each bullet's Collider2D immediately (safe during physics callbacks)
    /// to prevent ghost-collider hits on the frame the player re-activates,
    /// then schedules the GameObject for end-of-frame cleanup via Destroy().
    /// DestroyImmediate is forbidden during physics trigger/contact callbacks.
    /// </summary>
    private void DestroyAllProjectiles()
    {
        GameObject[] playerBullets = GameObject.FindGameObjectsWithTag("Player Bullet");
        foreach (GameObject bullet in playerBullets)
        {
            if (bullet == null) continue;
            // Disable the collider first so it can't trigger any more OnCollision/OnTrigger
            // events before the GameObject is actually removed at end-of-frame.
            Collider2D col = bullet.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            Destroy(bullet);
        }

        GameObject[] enemyBullets = GameObject.FindGameObjectsWithTag("Enemy Bullet");
        foreach (GameObject bullet in enemyBullets)
        {
            if (bullet == null) continue;
            Collider2D col = bullet.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            Destroy(bullet);
        }
    }

    /// <summary>
    /// Log episode statistics to console and CSV
    /// </summary>
    private void LogEpisodeStats()
    {
        float playerFinalHP = (playerHealthScript != null) ? playerHealthScript.currentHealth : 0f;
        float enemyFinalHP = (enemyHealthScript != null) ? enemyHealthScript.currentHealth : 0f;

        Debug.Log($"=== Episode {episodeCount} Stats ===");
        Debug.Log($"Duration: {timeSurvived:F2} seconds");
        Debug.Log($"Player Damage Dealt: {playerDamageDealt:F2}");
        Debug.Log($"Enemy Damage Dealt: {enemyDamageDealt:F2}");
        Debug.Log($"Player Final HP: {playerFinalHP}");
        Debug.Log($"Enemy Final HP: {enemyFinalHP}");
        Debug.Log("========================");

        DifficultyTier tier = WriteEpisodeCsv(playerFinalHP, enemyFinalHP);
        Debug.Log($"[FuzzyTier] Episode {episodeCount}: tier = {tier} ({(int)tier})");
    }

    private int MapOutcomeToInt(string outcome)
    {
        switch (outcome)
        {
            case "player_defeated": return 0;
            case "enemy_defeated": return 1;
            case "timeout": return 2;
            default: return 2;
        }
    }

    private void AddRecentOutcome(int numericOutcome)
    {
        // Only consider wins (1) vs not-wins (0) for rolling win rate. Map timeout as 0.
        int win = (numericOutcome == 1) ? 1 : 0;
        recentOutcomes.Enqueue(win);
        while (recentOutcomes.Count > RollingWindowSize)
            recentOutcomes.Dequeue();
    }

    private float GetRollingWinRate()
    {
        if (recentOutcomes.Count == 0) return 0f;
        int sum = 0;
        foreach (int v in recentOutcomes) sum += v;
        return (float)sum / recentOutcomes.Count;
    }

    private void AddRecentSurvivalTime(float survivalTime)
    {
        recentSurvivalTimes.Enqueue(survivalTime);
        while (recentSurvivalTimes.Count > RollingWindowSize)
            recentSurvivalTimes.Dequeue();
    }

    private float GetRollingAvgSurvivalTime()
    {
        if (recentSurvivalTimes.Count == 0) return 0f;
        float sum = 0f;
        foreach (float v in recentSurvivalTimes) sum += v;
        return sum / recentSurvivalTimes.Count;
    }

    /// <summary>
    /// Writes per-episode metrics to CSV, evaluates the fuzzy tier classifier,
    /// appends current_tier as the final column, and returns the resulting tier.
    /// </summary>
    private DifficultyTier WriteEpisodeCsv(float playerFinalHP, float enemyFinalHP)
    {
        // Read player telemetry totals (shots / hits / damage)
        int playerShots = playerTelemetry != null ? playerTelemetry.TotalShotsFired : 0;
        int playerHits = playerTelemetry != null ? playerTelemetry.TotalShotsHit : 0;
        float playerDamage = playerDamageDealt;
        float playerDamageTaken = enemyDamageDealt;

        // Enemy telemetry
        int enemyShots = enemyShotsFired;
        int enemyHits = enemyShotsHit;
        float enemyDamage = enemyDamageDealt;

        // Outcome mapping
        int outcomeNumeric = MapOutcomeToInt(lastEpisodeOutcome);

        // Derived metrics
        float playerAccuracy = playerShots > 0 ? (float)playerHits / playerShots : 0f;
        float enemyAccuracy = enemyShots > 0 ? (float)enemyHits / enemyShots : 0f;
        float damageRatio = Mathf.Approximately(playerDamageTaken, 0f)
            ? (playerDamage > 0f ? float.PositiveInfinity : 0f)
            : playerDamage / playerDamageTaken;

        // Update rolling outcomes and compute balance score
        AddRecentOutcome(outcomeNumeric);
        float rollingWinRate = GetRollingWinRate();
        float challengeBalanceScore = 1f - Mathf.Abs(rollingWinRate - 0.5f);

        // Add recent survival time and compute rolling average
        AddRecentSurvivalTime(timeSurvived);
        float avgSurvivalTime = GetRollingAvgSurvivalTime();

        // Use values captured before DestroyAllProjectiles() was called in EndEpisode().
        float pressure        = capturedPressure;
        float difficulty      = capturedDifficulty;
        int activeEnemyBullets = capturedActiveBullets;

        // Active difficulty values were snapshotted at episode start
        float fireRateValue = start_fireRate;
        float bulletSpeedValue = start_bulletSpeed;
        float spreadAngleValue = start_spreadAngle;
        float enemyMoveSpeedValue = start_enemyMoveSpeed;

        // ---- Fuzzy tier evaluation ----
        DifficultyTier currentTier = DifficultyTier.Medium; // default if classifier not wired
        if (_tierClassifier != null)
        {
            currentTier = _tierClassifier.Evaluate(rollingWinRate, avgSurvivalTime, episodeCount);
        }
        else
        {
            Debug.LogWarning("[RLTrainingManager] _tierClassifier is not assigned. Tier defaults to Medium.");
        }

        if (!writeEpisodeCsv)
            return currentTier;

        string directory = Path.Combine(Application.dataPath, "..", "Logs", logDirectoryName);
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, csvFileName);
        bool writeHeader = !File.Exists(path);

        using (StreamWriter writer = new StreamWriter(path, true))
        {
            if (writeHeader)
            {
                writer.WriteLine(string.Join(",",
                    "episode",
                    "outcome",
                    "survival_time",
                    "player_damage_dealt",
                    "enemy_damage_dealt",
                    "player_final_hp",
                    "enemy_final_hp",
                    "pressure",
                    "difficulty",
                    "active_enemy_bullets",
                    "shots_fired_player",
                    "shots_hit_player",
                    "player_accuracy",
                    "shots_fired_enemy",
                    "shots_hit_enemy",
                    "enemy_accuracy",
                    "damage_ratio",
                    "rolling_win_rate",
                    "challenge_balance_score",
                    "current_tier"
                ));
            }

            writer.WriteLine(string.Join(",",
                episodeCount.ToString(CultureInfo.InvariantCulture),
                lastEpisodeOutcome,
                timeSurvived.ToString("F3", CultureInfo.InvariantCulture),
                playerDamageDealt.ToString("F3", CultureInfo.InvariantCulture),
                enemyDamageDealt.ToString("F3", CultureInfo.InvariantCulture),
                playerFinalHP.ToString("F3", CultureInfo.InvariantCulture),
                enemyFinalHP.ToString("F3", CultureInfo.InvariantCulture),
                pressure.ToString("F3", CultureInfo.InvariantCulture),
                difficulty.ToString("F3", CultureInfo.InvariantCulture),
                activeEnemyBullets.ToString(CultureInfo.InvariantCulture),
                playerShots.ToString(CultureInfo.InvariantCulture),
                playerHits.ToString(CultureInfo.InvariantCulture),
                playerAccuracy.ToString("F3", CultureInfo.InvariantCulture),
                enemyShots.ToString(CultureInfo.InvariantCulture),
                enemyHits.ToString(CultureInfo.InvariantCulture),
                enemyAccuracy.ToString("F3", CultureInfo.InvariantCulture),
                float.IsInfinity(damageRatio) ? "inf" : damageRatio.ToString("F3", CultureInfo.InvariantCulture),
                rollingWinRate.ToString("F3", CultureInfo.InvariantCulture),
                challengeBalanceScore.ToString("F3", CultureInfo.InvariantCulture),
                ((int)currentTier).ToString(CultureInfo.InvariantCulture)
            ));
        }

        return currentTier;
    }

    /// <summary>
    /// Manually trigger episode end (useful for testing or ML-Agents integration)
    /// </summary>
    public void ForceEndEpisode()
    {
        EndEpisode();
    }

    public void ReportPlayerDamageDealt(float damage)
    {
        if (damage <= 0f)
            return;

        playerDamageDealt += damage;
    }

    public void ReportEnemyDamageDealt(float damage)
    {
        if (damage <= 0f)
            return;

        enemyDamageDealt += damage;
    }

    // New methods for enemy shot telemetry
    public void ReportEnemyShotFired()
    {
        enemyShotsFired++;
    }

    public void ReportEnemyShotHit()
    {
        enemyShotsHit++;
    }

    /// <summary>
    /// Get current player health percentage (0-1)
    /// </summary>
    public float GetPlayerHealthPercentage()
    {
        return playerHealthScript != null && playerHealthScript.maxHealth > 0 
            ? playerHealthScript.currentHealth / playerHealthScript.maxHealth 
            : 0f;
    }

    /// <summary>
    /// Get current enemy health percentage (0-1)
    /// </summary>
    public float GetEnemyHealthPercentage()
    {
        return enemyHealthScript != null && enemyHealthScript.maxHealth > 0 
            ? enemyHealthScript.currentHealth / enemyHealthScript.maxHealth 
            : 0f;
    }

    /// <summary>
    /// Get normalized episode progress (0-1)
    /// </summary>
    public float GetEpisodeProgress()
    {
        return episodeTimeLimit > 0 
            ? Mathf.Clamp01(currentEpisodeTime / episodeTimeLimit) 
            : 0f;
    }

    private void ApplyTrainingCondition()
    {
        if (DanmakuDDAController.Instance == null)
        {
            Debug.LogWarning("DanmakuDDAController.Instance is null, cannot apply training condition.");
            return;
        }

        switch (trainingCondition)
        {
            case TrainingCondition.StaticEasy:
                DanmakuDDAController.Instance.enabled = false;
                ApplyStaticDifficulty(0f); // 0 = minimum params
                break;
            case TrainingCondition.StaticHard:
                DanmakuDDAController.Instance.enabled = false;
                ApplyStaticDifficulty(1f); // 1 = maximum params
                break;
            case TrainingCondition.RuleBasedDDA:
                DanmakuDDAController.Instance.enabled = true;
                break;
        }
    }

    private void ApplyStaticDifficulty(float normalizedValue)
    {
        // Apply normalizedValue (0–1) directly to DifficultyProfile
        // to set all parameters to min (0) or max (1)
        DanmakuDDAController.Instance.ForceSetDifficulty(normalizedValue);
    }

    private string GetLogFileName()
    {
        return trainingCondition switch
        {
            TrainingCondition.StaticEasy    => "static_easy.csv",
            TrainingCondition.StaticHard    => "static_hard.csv",
            TrainingCondition.RuleBasedDDA  => "rule_based_dda.csv",
            _                               => "episode_log.csv"
        };
    }
}
