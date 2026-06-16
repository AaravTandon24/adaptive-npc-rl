using UnityEngine;

/// <summary>
/// Classifies the player into one of four difficulty tiers using fuzzy logic
/// membership functions applied to recent performance metrics.
///
/// Inputs (per episode end):
///   rollingWinRate  — float 0–1, win rate over last 10 episodes
///   playerFinalHP   — float 0–100, player HP at episode end
///   damageRatio     — float 0+, player damage dealt / enemy damage dealt
///
/// Call Evaluate() from RLTrainingManager at the end of each episode.
/// The classifier does NOT apply difficulty parameters — it only determines
/// and returns the current tier.
/// </summary>
public class FuzzyTierClassifier : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector-tweakable thresholds
    // -------------------------------------------------------------------------

    [Header("Cooldown")]
    [Tooltip("Number of episodes that must pass before the very first tier change is allowed.")]
    public int minEpisodesBeforeFirstChange = 10;

    [Tooltip("Number of episodes that must pass after a tier change before another is allowed.")]
    public int episodeCooldownBetweenChanges = 5;

    // -------------------------------------------------------------------------
    // Public state
    // -------------------------------------------------------------------------

    /// <summary>Current difficulty tier. Read-only for external scripts.</summary>
    public DifficultyTier CurrentTier { get; private set; } = DifficultyTier.Medium;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private int _lastChangeEpisode = -1;   // Episode index of the last tier change (-1 = never)

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates fuzzy membership functions against the supplied metrics and
    /// applies tier-transition rules. Returns the (potentially updated) tier.
    ///
    /// Must be called once per episode end from RLTrainingManager (or equivalent).
    /// </summary>
    /// <param name="rollingWinRate">Win rate over last 10 episodes (0–1).</param>
    /// <param name="avgSurvivalTime">Rolling average of survival time over last 10 episodes (0–60s).</param>
    /// <param name="episodeCount">Total episodes completed so far (1-indexed at end of episode).</param>
    /// <returns>The current <see cref="DifficultyTier"/> after evaluation.</returns>
    public DifficultyTier Evaluate(float rollingWinRate, float avgSurvivalTime, int episodeCount)
    {
        // ---- Guard: minimum warm-up period ----
        if (episodeCount < minEpisodesBeforeFirstChange)
        {
            Debug.Log($"[FuzzyTierClassifier] Episode {episodeCount}: warm-up period " +
                      $"({minEpisodesBeforeFirstChange} episodes required). Tier locked at {CurrentTier}.");
            return CurrentTier;
        }

        // ---- Guard: cooldown between changes ----
        if (_lastChangeEpisode >= 0 && episodeCount - _lastChangeEpisode < episodeCooldownBetweenChanges)
        {
            int remaining = episodeCooldownBetweenChanges - (episodeCount - _lastChangeEpisode);
            Debug.Log($"[FuzzyTierClassifier] Episode {episodeCount}: cooldown active " +
                      $"({remaining} episode(s) remaining). Tier locked at {CurrentTier}.");
            return CurrentTier;
        }

        // ---- Compute fuzzy memberships ----
        float wrHigh = WinRate_High(rollingWinRate);
        float stHigh = SurvivalTime_High(avgSurvivalTime);

        float wrLow  = WinRate_Low(rollingWinRate);
        float stLow  = SurvivalTime_Low(avgSurvivalTime);

        // ---- Fuzzy rule: Tier UP ----
        // Tier UP when: winRate_High AND survivalTime_High
        // min(winRate_High, survivalTime_High) > 0.6
        float tierUpStrength = Mathf.Min(wrHigh, stHigh);

        // ---- Fuzzy rule: Tier DOWN ----
        // Tier DOWN when: winRate_Low AND survivalTime_Low
        // min(winRate_Low, survivalTime_Low) > 0.6
        float tierDownStrength = Mathf.Min(wrLow, stLow);

        Debug.Log($"[FuzzyTierClassifier] Episode {episodeCount} | " +
                  $"WR={rollingWinRate:F2} SurvivalTime={avgSurvivalTime:F1} | " +
                  $"UpStrength={tierUpStrength:F3} DownStrength={tierDownStrength:F3} | " +
                  $"CurrentTier={CurrentTier}");

        // UP takes priority over DOWN if both fire simultaneously (unlikely but defensive)
        if (tierUpStrength > 0.6f && CurrentTier < DifficultyTier.Expert)
        {
            DifficultyTier previous = CurrentTier;
            CurrentTier = (DifficultyTier)((int)CurrentTier + 1);
            _lastChangeEpisode = episodeCount;
            Debug.Log($"[FuzzyTierClassifier] Tier UP: {previous} → {CurrentTier} " +
                      $"(strength={tierUpStrength:F3}, threshold=0.6)");
        }
        else if (tierDownStrength > 0.6f && CurrentTier > DifficultyTier.Easy)
        {
            DifficultyTier previous = CurrentTier;
            CurrentTier = (DifficultyTier)((int)CurrentTier - 1);
            _lastChangeEpisode = episodeCount;
            Debug.Log($"[FuzzyTierClassifier] Tier DOWN: {previous} → {CurrentTier} " +
                      $"(strength={tierDownStrength:F3}, threshold=0.6)");
        }

        return CurrentTier;
    }

    // -------------------------------------------------------------------------
    // Fuzzy membership functions — Win Rate
    // -------------------------------------------------------------------------

    /// <summary>
    /// winRate_Low: full membership below 0.2, zero above 0.4.
    /// </summary>
    public float WinRate_Low(float winRate)
    {
        if (winRate <= 0.2f) return 1f;
        if (winRate >= 0.4f) return 0f;
        return 1f - (winRate - 0.2f) / 0.2f;
    }

    /// <summary>
    /// winRate_Medium: peaks at 0.5, zero below 0.3 and above 0.7.
    /// </summary>
    public float WinRate_Medium(float winRate)
    {
        if (winRate <= 0.3f || winRate >= 0.7f) return 0f;
        if (winRate <= 0.5f) return (winRate - 0.3f) / 0.2f;
        return 1f - (winRate - 0.5f) / 0.2f;
    }

    /// <summary>
    /// winRate_High: zero below 0.6, full membership above 0.8.
    /// </summary>
    public float WinRate_High(float winRate)
    {
        if (winRate <= 0.6f) return 0f;
        if (winRate >= 0.8f) return 1f;
        return (winRate - 0.6f) / 0.2f;
    }

    // -------------------------------------------------------------------------
    // Fuzzy membership functions — Average Survival Time
    // -------------------------------------------------------------------------

    /// <summary>
    /// survivalTime_Low: full membership below 8s, zero above 15s.
    /// </summary>
    public float SurvivalTime_Low(float survivalTime)
    {
        if (survivalTime <= 8f) return 1f;
        if (survivalTime >= 15f) return 0f;
        return 1f - (survivalTime - 8f) / 7f;
    }

    /// <summary>
    /// survivalTime_Medium: peaks at 20s, zero below 10s and above 35s.
    /// </summary>
    public float SurvivalTime_Medium(float survivalTime)
    {
        if (survivalTime <= 10f || survivalTime >= 35f) return 0f;
        if (survivalTime <= 20f) return (survivalTime - 10f) / 10f;
        return 1f - (survivalTime - 20f) / 15f;
    }

    /// <summary>
    /// survivalTime_High: zero below 25s, full membership above 40s.
    /// </summary>
    public float SurvivalTime_High(float survivalTime)
    {
        if (survivalTime <= 25f) return 0f;
        if (survivalTime >= 40f) return 1f;
        return (survivalTime - 25f) / 15f;
    }
}
