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
    [Tooltip("Win-rate at which winRate_High starts to rise (>0).")]
    public float tierUpWinRateStart = 0.7f;
    [Tooltip("Win-rate at which winRate_High reaches full membership (1.0).")]
    public float tierUpWinRateEnd = 0.9f;

    [Tooltip("Player HP at which playerHP_High starts to rise (>0). Scale: 0-10.")]
    public float tierUpPlayerHPStart = 2.0f;
    [Tooltip("Player HP at which playerHP_High reaches full membership (1.0). Scale: 0-10.")]
    public float tierUpPlayerHPEnd = 5.0f;

    [Tooltip("Damage ratio at which damageRatio_High starts to rise (>0).")]
    public float tierUpDamageRatioStart = 5.0f;
    [Tooltip("Damage ratio at which damageRatio_High reaches full membership (1.0).")]
    public float tierUpDamageRatioEnd = 15.0f;

    [Tooltip("Minimum combined fuzzy membership required to trigger a tier-up.")]
    public float tierUpMembership = 0.6f;

    [Header("Tier DOWN Thresholds")]
    [Tooltip("Win-rate below which winRate_Low starts to rise (>0).")]
    public float tierDownWinRateStart = 0.5f;
    [Tooltip("Win-rate below which winRate_Low reaches full membership (1.0).")]
    public float tierDownWinRateEnd = 0.3f;

    [Tooltip("Player HP below which playerHP_Low starts to rise (>0). Scale: 0-10.")]
    public float tierDownPlayerHPStart = 3.0f;
    [Tooltip("Player HP below which playerHP_Low reaches full membership (1.0). Scale: 0-10.")]
    public float tierDownPlayerHPEnd = 1.0f;

    [Tooltip("Damage ratio below which damageRatio_Low starts to rise (>0).")]
    public float tierDownDamageRatioStart = 6.0f;
    [Tooltip("Damage ratio below which damageRatio_Low reaches full membership (1.0).")]
    public float tierDownDamageRatioEnd = 3.0f;

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
    /// winRate_Low: full membership (1) below tierDownWinRateEnd, zero above tierDownWinRateStart.
    /// Linear ramp between them.
    /// </summary>
    public float WinRate_Low(float winRate)
    {
        float range = tierDownWinRateStart - tierDownWinRateEnd;
        if (range <= 0f) return winRate <= tierDownWinRateEnd ? 1f : 0f;
        if (winRate <= tierDownWinRateEnd) return 1f;
        if (winRate >= tierDownWinRateStart) return 0f;
        return 1f - (winRate - tierDownWinRateEnd) / range;
    }

    /// <summary>
    /// winRate_Medium: triangular peak around the midpoint of Low and High thresholds.
    /// </summary>
    public float WinRate_Medium(float winRate)
    {
        float start = tierDownWinRateEnd;
        float peak = 0.5f;
        float end = tierUpWinRateEnd;

        if (winRate <= start || winRate >= end) return 0f;
        if (winRate <= peak) return (winRate - start) / (peak - start);
        return 1f - (winRate - peak) / (end - peak);
    }

    /// <summary>
    /// winRate_High: zero below tierUpWinRateStart, full membership (1) above tierUpWinRateEnd.
    /// </summary>
    public float WinRate_High(float winRate)
    {
        float range = tierUpWinRateEnd - tierUpWinRateStart;
        if (range <= 0f) return winRate >= tierUpWinRateEnd ? 1f : 0f;
        if (winRate <= tierUpWinRateStart) return 0f;
        if (winRate >= tierUpWinRateEnd) return 1f;
        return (winRate - tierUpWinRateStart) / range;
    }

    // -------------------------------------------------------------------------
    // Fuzzy membership functions — Player Final HP
    // -------------------------------------------------------------------------

    /// <summary>
    /// playerHP_Low: full membership (1) below tierDownPlayerHPEnd, zero above tierDownPlayerHPStart.
    /// </summary>
    public float PlayerHP_Low(float hp)
    {
        float range = tierDownPlayerHPStart - tierDownPlayerHPEnd;
        if (range <= 0f) return hp <= tierDownPlayerHPEnd ? 1f : 0f;
        if (hp <= tierDownPlayerHPEnd) return 1f;
        if (hp >= tierDownPlayerHPStart) return 0f;
        return 1f - (hp - tierDownPlayerHPEnd) / range;
    }

    /// <summary>
    /// playerHP_Medium: triangular peak around 5.0, zero below 2.0 and above 8.0.
    /// </summary>
    public float PlayerHP_Medium(float hp)
    {
        if (hp <= 2f || hp >= 8f) return 0f;
        if (hp <= 5f) return (hp - 2f) / (5f - 2f);
        return 1f - (hp - 5f) / (8f - 5f);
    }

    /// <summary>
    /// playerHP_High: zero below tierUpPlayerHPStart, full membership (1) above tierUpPlayerHPEnd.
    /// </summary>
    public float PlayerHP_High(float hp)
    {
        float range = tierUpPlayerHPEnd - tierUpPlayerHPStart;
        if (range <= 0f) return hp >= tierUpPlayerHPEnd ? 1f : 0f;
        if (hp <= tierUpPlayerHPStart) return 0f;
        if (hp >= tierUpPlayerHPEnd) return 1f;
        return (hp - tierUpPlayerHPStart) / range;
    }

    // -------------------------------------------------------------------------
    // Fuzzy membership functions — Damage Ratio
    // -------------------------------------------------------------------------

    /// <summary>
    /// damageRatio_Low: full membership (1) below tierDownDamageRatioEnd, zero above tierDownDamageRatioStart.
    /// </summary>
    public float DamageRatio_Low(float ratio)
    {
        float range = tierDownDamageRatioStart - tierDownDamageRatioEnd;
        if (range <= 0f) return ratio <= tierDownDamageRatioEnd ? 1f : 0f;
        if (ratio <= tierDownDamageRatioEnd) return 1f;
        if (ratio >= tierDownDamageRatioStart) return 0f;
        return 1f - (ratio - tierDownDamageRatioEnd) / range;
    }

    /// <summary>
    /// damageRatio_Medium: triangular peak around 1.0, zero below 0.5 and above 1.5.
    /// </summary>
    public float DamageRatio_Medium(float ratio)
    {
        if (ratio <= 0.5f || ratio >= 1.5f) return 0f;
        if (ratio <= 1.0f) return (ratio - 0.5f) / (1.0f - 0.5f);
        return 1f - (ratio - 1.0f) / (1.5f - 1.0f);
    }

    /// <summary>
    /// damageRatio_High: zero below tierUpDamageRatioStart, full membership (1) above tierUpDamageRatioEnd.
    /// </summary>
    public float DamageRatio_High(float ratio)
    {
        float range = tierUpDamageRatioEnd - tierUpDamageRatioStart;
        if (range <= 0f) return ratio >= tierUpDamageRatioEnd ? 1f : 0f;
        if (ratio <= tierUpDamageRatioStart) return 0f;
        if (ratio >= tierUpDamageRatioEnd) return 1f;
        return (ratio - tierUpDamageRatioStart) / range;
    }
}
