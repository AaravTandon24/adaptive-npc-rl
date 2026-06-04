using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// GameManager specifically designed for Reinforcement Learning training environment.
/// Manages episodes between Player and Enemy without scene reloads.
/// </summary>
public class RLTrainingManager : MonoBehaviour
{
    [Header("Entity References")]
    [Tooltip("Reference to the Player GameObject")]
    public GameObject player;
    
    [Tooltip("Reference to the Enemy GameObject")]
    public GameObject enemy;

    [Header("Episode Settings")]
    [Tooltip("Maximum duration of an episode in seconds")]
    public float episodeTimeLimit = 60f;

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

    // Private cached references
    private Vector3 playerStartPosition;
    private Vector3 enemyStartPosition;
    private PlayerLivesScript playerHealthScript;
    private TestEnemyHealthScript enemyHealthScript;
    private EnemyAgent enemyAgent;
    private bool episodeActive = false;
    private string lastEpisodeOutcome = "unknown";

    void Start()
    {
        InitializeEpisode();
    }

    void Update()
    {
        if (!episodeActive)
            return;

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

        if (playerHealthScript == null)
        {
            Debug.LogError("RLTrainingManager: PlayerLivesScript not found on Player!");
            return;
        }

        if (enemyHealthScript == null)
        {
            Debug.LogError("RLTrainingManager: TestEnemyHealthScript not found on Enemy!");
            return;
        }

        // Start first episode
        ResetEpisode();
        episodeActive = true;
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
            EndEpisode();
            return;
        }

        // Condition 1: Player HP reaches 0
        if (playerHealthScript.currentHealth <= 0)
        {
            Debug.Log("Episode ended: Player defeated");
            lastEpisodeOutcome = "player_defeated";
            EndEpisode();
            return;
        }

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

        // Log episode statistics
        LogEpisodeStats();

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
        // Reset timer
        currentEpisodeTime = 0f;
        lastEpisodeOutcome = "unknown";

        // Reset performance metrics
        playerDamageDealt = 0f;
        enemyDamageDealt = 0f;
        timeSurvived = 0f;

        // Destroy all projectiles FIRST before resetting entities
        DestroyAllProjectiles();

        // Reset player
        if (player != null)
        {
            player.transform.position = playerStartPosition;
            
            // Reset health WITHOUT triggering GameOver
            playerHealthScript.currentHealth = playerHealthScript.maxHealth;
            
            // Ensure player is active and Time.timeScale is normal
            if (!player.activeInHierarchy)
                player.SetActive(true);
            
            // Ensure time is not paused (in case GameOver was triggered)
            Time.timeScale = 1f;
            
            // Reset the isDead flag via reflection (since it's private)
            var isDead = typeof(PlayerLivesScript).GetField("isDead", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isDead != null)
                isDead.SetValue(playerHealthScript, false);
        }

        // Reset enemy
        if (enemy != null)
        {
            enemy.transform.position = enemyStartPosition;
            enemyHealthScript.ResetHealth();
            
            if (!enemy.activeInHierarchy)
                enemy.SetActive(true);
        }
        else
        {
            Debug.LogError("Enemy is null! Cannot reset episode. Make sure enemy is not being destroyed.");
        }

        Debug.Log($"Episode {episodeCount + 1} started");
    }

    /// <summary>
    /// Destroy all active projectiles in the scene
    /// </summary>
    private void DestroyAllProjectiles()
    {
        // Destroy all player bullets
        GameObject[] playerBullets = GameObject.FindGameObjectsWithTag("Player Bullet");
        foreach (GameObject bullet in playerBullets)
        {
            Destroy(bullet);
        }

        // Destroy all enemy bullets
        GameObject[] enemyBullets = GameObject.FindGameObjectsWithTag("Enemy Bullet");
        foreach (GameObject bullet in enemyBullets)
        {
            Destroy(bullet);
        }
    }

    /// <summary>
    /// Log episode statistics to console
    /// </summary>
    private void LogEpisodeStats()
    {
        Debug.Log($"=== Episode {episodeCount} Stats ===");
        Debug.Log($"Duration: {timeSurvived:F2} seconds");
        Debug.Log($"Player Damage Dealt: {playerDamageDealt:F2}");
        Debug.Log($"Enemy Damage Dealt: {enemyDamageDealt:F2}");
        
        float playerFinalHP = (playerHealthScript != null) ? playerHealthScript.currentHealth : 0f;
        float enemyFinalHP = (enemyHealthScript != null) ? enemyHealthScript.currentHealth : 0f;
        
        Debug.Log($"Player Final HP: {playerFinalHP}");
        Debug.Log($"Enemy Final HP: {enemyFinalHP}");
        Debug.Log("========================");

        WriteEpisodeCsv(playerFinalHP, enemyFinalHP);
    }

    private void WriteEpisodeCsv(float playerFinalHP, float enemyFinalHP)
    {
        if (!writeEpisodeCsv)
            return;

        string directory = Path.Combine(Application.dataPath, "..", "Logs", logDirectoryName);
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, csvFileName);
        bool writeHeader = !File.Exists(path);

        float pressure = DanmakuDDAController.Instance != null ? DanmakuDDAController.Instance.currentPressure : 0f;
        float difficulty = DanmakuDDAController.Instance != null ? DanmakuDDAController.Instance.currentDifficulty : 0f;
        int activeEnemyBullets = GameObject.FindGameObjectsWithTag("Enemy Bullet").Length;

        using (StreamWriter writer = new StreamWriter(path, true))
        {
            if (writeHeader)
            {
                writer.WriteLine("episode,outcome,survival_time,player_damage_dealt,enemy_damage_dealt,player_final_hp,enemy_final_hp,pressure,difficulty,active_enemy_bullets");
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
                activeEnemyBullets.ToString(CultureInfo.InvariantCulture)));
        }
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
}
