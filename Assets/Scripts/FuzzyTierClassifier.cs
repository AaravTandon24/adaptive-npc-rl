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

    [Header("Tier UP Thresholds")]
    [Tooltip("Win-rate value at which winRate_High reaches full membership (1.0).")]
    public float tierUpWinRate = 0.7f;

    [Tooltip("Player HP value at which playerHP_High reaches full membership (1.0).")]
    public float tierUpPlayerHP = 60f;

    [Tooltip("Damage ratio value at which damageRatio_High reaches full membership (1.0).")]
    public float tierUpDamageRatio = 1.3f;

    [Tooltip("Minimum combined fuzzy membership required to trigger a tier-up.")]
    public float tierUpMembership = 0.6f;

    [Header("Tier DOWN Thresholds")]
    [Tooltip("Win-rate value below which winRate_Low reaches full membership (1.0).")]
    public float tierDownWinRate = 0.3f;

    [Tooltip("Player HP value below which playerHP_Low reaches full membership (1.0).")]
    public float tierDownPlayerHP = 20f;

    [Tooltip("Damage ratio value below which damageRatio_Low reaches full membership (1.0).")]
    public float tierDownDamageRatio = 0.7f;

    [Tooltip("Minimum combined fuzzy membership required to trigger a tier-down.")]
    public float tierDownMembership = 0.6f;

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
    /// <param name="playerFinalHP">Player HP at episode end (0–100).</param>
    /// <param name="damageRatio">Player damage dealt / enemy damage dealt (0+).</param>
    /// <param name="episodeCount">Total episodes completed so far (1-indexed at end of episode).</param>
    /// <returns>The current <see cref="DifficultyTier"/> after evaluation.</returns>
    public DifficultyTier Evaluate(float rollingWinRate, float playerFinalHP, float damageRatio, int episodeCount)
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
        float wrHigh  = WinRate_High(rollingWinRate);
        float hpHigh  = PlayerHP_High(playerFinalHP);
        float drHigh  = DamageRatio_High(damageRatio);

        float wrLow   = WinRate_Low(rollingWinRate);
        float hpLow   = PlayerHP_Low(playerFinalHP);
        float drLow   = DamageRatio_Low(damageRatio);

        // ---- Fuzzy rule: Tier UP ----
        // Rule: winRate_High AND playerHP_High AND damageRatio_High → combined = min of all three
        float tierUpStrength = Mathf.Min(wrHigh, Mathf.Min(hpHigh, drHigh));

        // ---- Fuzzy rule: Tier DOWN ----
        // Rule: winRate_Low AND playerHP_Low AND damageRatio_Low → combined = min of all three
        float tierDownStrength = Mathf.Min(wrLow, Mathf.Min(hpLow, drLow));

        Debug.Log($"[FuzzyTierClassifier] Episode {episodeCount} | " +
                  $"WR={rollingWinRate:F2} HP={playerFinalHP:F1} DR={damageRatio:F2} | " +
                  $"UpStrength={tierUpStrength:F3} DownStrength={tierDownStrength:F3} | " +
                  $"CurrentTier={CurrentTier}");

        // UP takes priority over DOWN if both fire simultaneously (unlikely but defensive)
        if (tierUpStrength > tierUpMembership && CurrentTier < DifficultyTier.Expert)
        {
            DifficultyTier previous = CurrentTier;
            CurrentTier = (DifficultyTier)((int)CurrentTier + 1);
            _lastChangeEpisode = episodeCount;
            Debug.Log($"[FuzzyTierClassifier] Tier UP: {previous} → {CurrentTier} " +
                      $"(strength={tierUpStrength:F3}, threshold={tierUpMembership})");
        }
        else if (tierDownStrength > tierDownMembership && CurrentTier > DifficultyTier.Easy)
        {
            DifficultyTier previous = CurrentTier;
            CurrentTier = (DifficultyTier)((int)CurrentTier - 1);
            _lastChangeEpisode = episodeCount;
            Debug.Log($"[FuzzyTierClassifier] Tier DOWN: {previous} → {CurrentTier} " +
                      $"(strength={tierDownStrength:F3}, threshold={tierDownMembership})");
        }

        return CurrentTier;
    }

    // -------------------------------------------------------------------------
    // Fuzzy membership functions — Win Rate
    // -------------------------------------------------------------------------

    /// <summary>
    /// winRate_Low: full membership (1) below 0.3, zero above 0.5.
    /// Linear ramp from 1 at 0.3 down to 0 at 0.5.
    /// </summary>
    public float WinRate_Low(float winRate)
    {
        // Shoulders: [0, 0.3] → 1.0 ; [0.3, 0.5] → linear decay ; [0.5, 1] → 0.0
        if (winRate <= 0.3f) return 1f;
        if (winRate >= 0.5f) return 0f;
        return 1f - (winRate - 0.3f) / (0.5f - 0.3f);
    }

    /// <summary>
    /// winRate_Medium: triangular peak at 0.5, zero at or below 0.3 and at or above 0.7.
    /// </summary>
    public float WinRate_Medium(float winRate)
    {
        // Rising: [0.3, 0.5] ; Falling: [0.5, 0.7]
        if (winRate <= 0.3f || winRate >= 0.7f) return 0f;
        if (winRate <= 0.5f) return (winRate - 0.3f) / (0.5f - 0.3f);
        return 1f - (winRate - 0.5f) / (0.7f - 0.5f);
    }

    /// <summary>
    /// winRate_High: zero below 0.5, full membership (1) above 0.7.
    /// Linear ramp from 0 at 0.5 up to 1 at 0.7.
    /// </summary>
    public float WinRate_High(float winRate)
    {
        // Shoulders: [0, 0.5] → 0.0 ; [0.5, 0.7] → linear rise ; [0.7, 1] → 1.0
        if (winRate <= 0.5f) return 0f;
        if (winRate >= 0.7f) return 1f;
        return (winRate - 0.5f) / (0.7f - 0.5f);
    }

    // -------------------------------------------------------------------------
    // Fuzzy membership functions — Player Final HP
    // -------------------------------------------------------------------------

    /// <summary>
    /// playerHP_Low: full membership (1) below 20, zero above 40.
    /// Linear ramp from 1 at 20 down to 0 at 40.
    /// </summary>
    public float PlayerHP_Low(float hp)
    {
        if (hp <= 20f) return 1f;
        if (hp >= 40f) return 0f;
        return 1f - (hp - 20f) / (40f - 20f);
    }

    /// <summary>
    /// playerHP_Medium: triangular peak at 50, zero at or below 20 and at or above 80.
    /// </summary>
    public float PlayerHP_Medium(float hp)
    {
        if (hp <= 20f || hp >= 80f) return 0f;
        if (hp <= 50f) return (hp - 20f) / (50f - 20f);
        return 1f - (hp - 50f) / (80f - 50f);
    }

    /// <summary>
    /// playerHP_High: zero below 60, full membership (1) above 80.
    /// Linear ramp from 0 at 60 up to 1 at 80.
    /// </summary>
    public float PlayerHP_High(float hp)
    {
        if (hp <= 60f) return 0f;
        if (hp >= 80f) return 1f;
        return (hp - 60f) / (80f - 60f);
    }

    // -------------------------------------------------------------------------
    // Fuzzy membership functions — Damage Ratio
    // -------------------------------------------------------------------------

    /// <summary>
    /// damageRatio_Low: full membership (1) below 0.5, zero above 1.0.
    /// Linear ramp from 1 at 0.5 down to 0 at 1.0.
    /// </summary>
    public float DamageRatio_Low(float ratio)
    {
        if (ratio <= 0.5f) return 1f;
        if (ratio >= 1.0f) return 0f;
        return 1f - (ratio - 0.5f) / (1.0f - 0.5f);
    }

    /// <summary>
    /// damageRatio_Medium: triangular peak at 1.0, zero at or below 0.5 and at or above 1.5.
    /// </summary>
    public float DamageRatio_Medium(float ratio)
    {
        if (ratio <= 0.5f || ratio >= 1.5f) return 0f;
        if (ratio <= 1.0f) return (ratio - 0.5f) / (1.0f - 0.5f);
        return 1f - (ratio - 1.0f) / (1.5f - 1.0f);
    }

    /// <summary>
    /// damageRatio_High: zero below 1.0, full membership (1) above 1.5.
    /// Linear ramp from 0 at 1.0 up to 1 at 1.5.
    /// </summary>
    public float DamageRatio_High(float ratio)
    {
        if (ratio <= 1.0f) return 0f;
        if (ratio >= 1.5f) return 1f;
        return (ratio - 1.0f) / (1.5f - 1.0f);
    }
}
