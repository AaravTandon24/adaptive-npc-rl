using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// Agent 2: RL-based DDA controller.
///
/// Makes one difficulty decision per game episode. Observes player performance
/// metrics and outputs 4 continuous multiplier values (fireRate, bulletSpeed,
/// spreadAngle, enemySpeed), constrained to the current tier's bounds.
///
/// Reward design targets a 50% win rate (challenge_balance_score).
///
/// Notes:
/// - Does NOT use DecisionRequester — decisions are triggered manually by
///   RLTrainingManager at the end of each game episode.
/// - A "meta-episode" spans multiple game episodes (default 10). The agent
///   accumulates reward across game episodes and PPO updates at meta-episode end.
/// - Tier bounds come from DifficultyProfile.GetBoundsForTier(), which is
///   derived from the same Lerp ranges used by the rule-based DDA system.
/// </summary>
public class DDAAgent : Agent
{
    [Header("References")]
    [Tooltip("RLTrainingManager — provides player metrics and episode outcomes")]
    public RLTrainingManager trainingManager;

    [Tooltip("FuzzyTierClassifier — provides the current difficulty tier")]
    public FuzzyTierClassifier tierClassifier;

    [Header("Meta-Episode")]
    [Tooltip("Number of game episodes per DDAAgent meta-episode")]
    public int metaEpisodeLength = 10;


    [Header("Reward Tuning")]
    [Tooltip("Weight for the closeness bonus (HP parity at episode end). Set to 0 to disable.")]
    public float closenessBonusWeight = 0.5f;

    [Tooltip("Penalty for blowout outcomes (one side dominant). Set to 0 to disable.")]
    public float blowoutPenalty = 0f;

    [Tooltip("HP threshold above which a win is considered a blowout")]
    public float blowoutHPThreshold = 0.8f;

    [Tooltip("Penalty applied when continuous outputs are at extremes (0 or 1) to prevent cheese strategies.")]
    public float extremeValuePenaltyWeight = 0.08f;

    [Tooltip("How strongly bullet count tracks the average of the 3 bullet-behaviour multipliers. " +
             "0 = always 1 bullet, 1 = full 1-2x range tracking. Default 0.5 = gentle scaling.")]
    [Range(0f, 1f)]
    public float bulletCountResponseScale = 0.5f;

    [Tooltip("How much enemy speed moves toward the agent's target per episode (0=frozen, 1=instant). " +
             "Lower values create a smooth lag that prevents jarring speed snaps.")]
    [Range(0.05f, 1f)]
    public float enemySpeedSmoothRate = 0.25f;

    [Tooltip("The window size for computing the win rate used for the step reward")]
    public int rewardWindowSize = 10;

    [Header("Debug")]
    [Tooltip("Log actions, multipliers, and rewards to the Unity Console every step")]
    public bool debugLogging = true;

    [Header("Runtime State (Read-Only)")]
    [Range(0f, 1f)] public float currentNormalizedFireRate = 0.5f;
    [Range(0f, 1f)] public float currentNormalizedBulletSpeed = 0.5f;
    [Range(0f, 1f)] public float currentNormalizedSpreadAngle = 0.5f;
    [Range(0f, 1f)] public float currentNormalizedEnemySpeed = 0.5f;

    // Smoothed enemy speed — lerps toward currentNormalizedEnemySpeed each episode
    // to prevent jarring speed snaps visible to the player.
    [Range(0f, 1f)] public float smoothedEnemySpeed = 0.5f;

    public float currentFireRate => Mathf.Lerp(currentBounds.fireRateMin, currentBounds.fireRateMax, currentNormalizedFireRate);
    public float currentBulletSpeed => Mathf.Lerp(currentBounds.bulletSpeedMin, currentBounds.bulletSpeedMax, currentNormalizedBulletSpeed);
    public float currentSpreadAngle => Mathf.Lerp(currentBounds.spreadAngleMin, currentBounds.spreadAngleMax, currentNormalizedSpreadAngle);
    public float currentEnemySpeed => Mathf.Lerp(currentBounds.enemySpeedMin, currentBounds.enemySpeedMax, currentNormalizedEnemySpeed);

    // Internal state
    private int gameEpisodesInMetaEpisode = 0;
    private DifficultyProfile.MultiplierBounds currentBounds;
    private DifficultyTier currentTier = DifficultyTier.Medium;

    public override void Initialize()
    {
        // Initialize with Medium tier bounds
        currentTier = tierClassifier != null ? tierClassifier.CurrentTier : DifficultyTier.Medium;
        currentBounds = DifficultyProfile.GetBoundsForTier(currentTier);
    }

    public override void OnEpisodeBegin()
    {
        gameEpisodesInMetaEpisode = 0;

        // Refresh tier and bounds at meta-episode start (multipliers persist for training continuity)
        currentTier = tierClassifier != null ? tierClassifier.CurrentTier : DifficultyTier.Medium;
        currentBounds = DifficultyProfile.GetBoundsForTier(currentTier);

        // Request the first difficulty decision for the beginning of the meta-episode
        RequestDecision();
    }

    /// <summary>
    /// 10 observations for the DDA policy.
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (sensor == null) return;

        // 1. Rolling win rate (1)
        float winRate = trainingManager != null ? trainingManager.GetRollingWinRate() : 0.5f;
        sensor.AddObservation(winRate);

        // 2. Rolling avg survival time, normalized by episode limit (1)
        float avgSurvival  = trainingManager != null ? trainingManager.GetRollingAvgSurvivalTime() : 30f;
        float episodeLimit = trainingManager != null ? trainingManager.episodeTimeLimit : 60f;
        sensor.AddObservation(Mathf.Clamp01(avgSurvival / episodeLimit));

        // 3. Last episode outcome: 0=player died, 1=enemy died, 2=timeout (1)
        int outcome = trainingManager != null ? trainingManager.GetLastEpisodeOutcomeNumeric() : 2;
        sensor.AddObservation(outcome / 2f); // normalize to 0-1

        // 4. Player final HP (normalized 0-1) (1)
        float playerHP = trainingManager != null ? trainingManager.GetLastEpisodePlayerHPPercentage() : 0.5f;
        sensor.AddObservation(playerHP);

        // 5. Enemy final HP (normalized 0-1) (1)
        float enemyHP = trainingManager != null ? trainingManager.GetLastEpisodeEnemyHPPercentage() : 0.5f;
        sensor.AddObservation(enemyHP);

        // 6. Current tier (normalized 0-1) (1)
        sensor.AddObservation((float)currentTier / 3f);

        // 7-10. Current multipliers, normalized within tier bounds (4)
        sensor.AddObservation(currentNormalizedFireRate);
        sensor.AddObservation(currentNormalizedBulletSpeed);
        sensor.AddObservation(currentNormalizedSpreadAngle);
        sensor.AddObservation(currentNormalizedEnemySpeed);

        // Total: 1+1+1+1+1+1+4 = 10
    }


    /// <summary>
    /// 4 continuous actions mapped to relative multiplier step deltas within tier bounds.
    /// Called once per game episode (manually triggered by RLTrainingManager).
    /// </summary>
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Refresh tier bounds (tier may have changed since last decision)
        currentTier = tierClassifier != null ? tierClassifier.CurrentTier : DifficultyTier.Medium;
        currentBounds = DifficultyProfile.GetBoundsForTier(currentTier);

        // Retrieve continuous actions in [-1, 1]
        float a0 = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float a1 = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float a2 = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);
        float a3 = Mathf.Clamp(actions.ContinuousActions[3], -1f, 1f);

        // Apply absolute actions mapped from [-1, 1] to normalized space [0, 1]
        currentNormalizedFireRate    = (a0 + 1f) / 2f;
        currentNormalizedBulletSpeed = (a1 + 1f) / 2f;
        currentNormalizedSpreadAngle = (a2 + 1f) / 2f;
        currentNormalizedEnemySpeed  = (a3 + 1f) / 2f;

        // Smooth enemy speed toward the agent's target — prevents jarring per-episode snaps.
        smoothedEnemySpeed = Mathf.Lerp(smoothedEnemySpeed, currentNormalizedEnemySpeed, enemySpeedSmoothRate);

        // Bullet count is derived from the average of the 3 bullet-behaviour multipliers,
        // scaled by bulletCountResponseScale so it rises gently without huge jumps.
        float avgBulletPressure = (currentNormalizedFireRate + currentNormalizedBulletSpeed + currentNormalizedSpreadAngle) / 3f;
        float bulletCountMultiplierDerived = Mathf.Lerp(1f, 2f, avgBulletPressure * bulletCountResponseScale);

        // Build a DifficultyProfile from the agent's outputs
        DifficultyProfile profile = DifficultyProfile.Default;
        profile.fireRateMultiplier    = currentFireRate;
        profile.bulletSpeedMultiplier = currentBulletSpeed;
        profile.spreadAngleMultiplier = currentSpreadAngle;
        // Use the smoothed speed so the player perceives a gradual change.
        profile.enemySpeedMultiplier  = Mathf.Lerp(currentBounds.enemySpeedMin, currentBounds.enemySpeedMax, smoothedEnemySpeed);

        // Keep spawn/powerup multipliers at the tier midpoint.
        float tierMidDifficulty = ((int)currentTier * 0.25f) + 0.125f;
        profile.spawnIntervalMultiplier = Mathf.Lerp(1.55f, 0.65f, tierMidDifficulty);
        profile.powerupSpawnMultiplier  = Mathf.Lerp(0.75f, 1.55f, 1f - tierMidDifficulty);
        // Bullet count derived from bullet-behaviour average — not a direct agent output.
        profile.bulletCountMultiplier   = bulletCountMultiplierDerived;

        if (DanmakuDDAController.Instance != null)
        {
            profile.maxActiveEnemyBullets = DanmakuDDAController.Instance.maxActiveEnemyBullets;
            DanmakuDDAController.Instance.ApplyAgentProfile(profile);
        }

        if (debugLogging)
        {
            int episodeNum = trainingManager != null ? trainingManager.episodeCount : 0;
            Debug.Log($"[DDAAgent Step] Game Episode {episodeNum} | Action: [{a0:F2}, {a1:F2}, {a2:F2}, {a3:F2}] | " +
                      $"Tier: {currentTier} | FireRate={profile.fireRateMultiplier:F2}, " +
                      $"BulletSpeed={profile.bulletSpeedMultiplier:F2}, SpreadAngle={profile.spreadAngleMultiplier:F2}, " +
                      $"EnemySpeed(smoothed)={profile.enemySpeedMultiplier:F2} (target={currentEnemySpeed:F2}), " +
                      $"BulletCount={profile.bulletCountMultiplier:F2}");
        }
    }

    /// <summary>
    /// Called by RLTrainingManager at the end of each game episode.
    /// Computes reward and decides whether to end the meta-episode.
    /// </summary>
    public void OnGameEpisodeEnd()
    {
        gameEpisodesInMetaEpisode++;

        // --- Compute reward for this game episode ---
        float winRate = 0.5f;
        if (trainingManager != null)
        {
            winRate = trainingManager.GetRecentWinRate(rewardWindowSize);
        }

        // Primary: reward for balanced win rate over short window (peaks at 0.5)
        float balanceReward = 1f - 2f * Mathf.Abs(winRate - 0.5f);
        AddReward(balanceReward);
        float totalAddedReward = balanceReward;

        // Secondary: closeness bonus
        float closenessBonusVal = 0f;
        if (closenessBonusWeight > 0f)
        {
            float playerHP = trainingManager != null ? trainingManager.GetPlayerHealthPercentage() : 0.5f;
            float enemyHP = trainingManager != null ? trainingManager.GetEnemyHealthPercentage() : 0.5f;
            closenessBonusVal = 1f - Mathf.Abs(playerHP - enemyHP);
            float added = closenessBonusWeight * closenessBonusVal;
            AddReward(added);
            totalAddedReward += added;
        }

        // Penalty for extreme values (cheese prevention)
        float extremePenaltyVal = 0f;
        if (extremeValuePenaltyWeight > 0f)
        {
            float p0 = Mathf.Abs(currentNormalizedFireRate - 0.5f) * 2f;
            float p1 = Mathf.Abs(currentNormalizedBulletSpeed - 0.5f) * 2f;
            float p2 = Mathf.Abs(currentNormalizedSpreadAngle - 0.5f) * 2f;
            float p3 = Mathf.Abs(currentNormalizedEnemySpeed - 0.5f) * 2f;
            float avgExtreme = (p0 + p1 + p2 + p3) / 4f;
            
            extremePenaltyVal = -extremeValuePenaltyWeight * avgExtreme;
            AddReward(extremePenaltyVal);
            totalAddedReward += extremePenaltyVal;
        }

        if (debugLogging)
        {
            string outcomeStr = trainingManager != null ? trainingManager.lastEpisodeOutcome : "unknown";
            int episodeNum = trainingManager != null ? trainingManager.episodeCount : 0;
            Debug.Log($"[DDAAgent Reward] Game Episode {episodeNum} | Outcome: {outcomeStr} | WinRate ({rewardWindowSize}-ep window): {winRate:P0} | " +
                      $"Balance Reward: {balanceReward:F3} | Closeness Bonus: {closenessBonusVal * closenessBonusWeight:F3} | " +
                      $"Extreme Penalty: {extremePenaltyVal:F3} | Step Reward: {totalAddedReward:F3} | Total Cumulative Reward: {GetCumulativeReward():F3}");
        }

        // --- Meta-episode boundary ---
        if (gameEpisodesInMetaEpisode >= metaEpisodeLength)
        {
            EndEpisode(); // triggers PPO update
        }
        else
        {
            // Request the next difficulty decision for the upcoming game episode
            RequestDecision();
        }
    }

    /// <summary>
    /// Heuristic for testing — outputs midpoint values (all zeros → mid-range multipliers).
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = 0f; // mid-range fireRate
        continuous[1] = 0f; // mid-range bulletSpeed
        continuous[2] = 0f; // mid-range spreadAngle
        continuous[3] = 0f; // mid-range enemySpeed
    }
}
