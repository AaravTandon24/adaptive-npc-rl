using UnityEngine;

public class DanmakuDDAController : MonoBehaviour
{
    public static DanmakuDDAController Instance { get; private set; }

    [Header("References")]
    public PlayerPerformanceTelemetry telemetry;
    public BulletPressureAnalyzer pressureAnalyzer;

    [Header("Target Pressure")]
    [Range(0f, 1f)] public float minTargetPressure = 0.45f;
    [Range(0f, 1f)] public float maxTargetPressure = 0.65f;

    [Header("Adaptation")]
    public float updateInterval = 2f;
    public float maxStepChange = 0.08f;
    public int maxActiveEnemyBullets = 120;
    public bool debugLogging = false;

    [Header("Runtime State")]
    [Range(0f, 1f)] public float currentDifficulty = 0.5f;
    [Range(0f, 1f)] public float currentPressure;
    public DifficultyProfile currentProfile = DifficultyProfile.Default;

    private RLTrainingManager trainingManager;
    private int lastChangedEpisode = -100;

    // Current difficulty tier — driven by FuzzyTierClassifier via SetTier().
    private DifficultyTier _currentTier = DifficultyTier.Medium;
    public DifficultyTier CurrentTier => _currentTier;

    // BUG-04 fix: maintain an explicit list of registered tunables so
    // ApplyProfileToScene() never needs FindObjectsOfType at runtime.
    private readonly System.Collections.Generic.List<IDifficultyTunable> _tunables =
        new System.Collections.Generic.List<IDifficultyTunable>();

    public DifficultyProfile CurrentProfile => currentProfile;

    private float nextUpdateTime;

    public static DanmakuDDAController EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject controllerObject = new GameObject("Danmaku DDA Controller");
        return controllerObject.AddComponent<DanmakuDDAController>();
    }

    private void Awake()
    {
        Instance = this;
        trainingManager = FindObjectOfType<RLTrainingManager>();

        if (telemetry == null)
            telemetry = FindObjectOfType<PlayerPerformanceTelemetry>();

        if (telemetry == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                telemetry = playerObject.AddComponent<PlayerPerformanceTelemetry>();
        }

        if (pressureAnalyzer == null)
            pressureAnalyzer = FindObjectOfType<BulletPressureAnalyzer>();

        if (pressureAnalyzer == null)
            pressureAnalyzer = gameObject.AddComponent<BulletPressureAnalyzer>();

        if (pressureAnalyzer.telemetry == null)
            pressureAnalyzer.telemetry = telemetry;

        currentProfile = DifficultyProfile.FromPressure(currentDifficulty, maxActiveEnemyBullets);
    }

    private void Start()
    {
        ApplyProfileToScene();
    }

    private void Update()
    {
        if (trainingManager != null)
            return;

        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + Mathf.Max(0.25f, updateInterval);
        MonitorAnalyzePlanExecute();
    }

    public void RegisterTunable(IDifficultyTunable tunable)
    {
        if (tunable == null) return;
        if (!_tunables.Contains(tunable))
            _tunables.Add(tunable);
        tunable.ApplyDifficulty(currentProfile);
    }

    private void MonitorAnalyzePlanExecute()
    {
        currentPressure = pressureAnalyzer != null ? pressureAnalyzer.GetPressureScore() : 0f;
        PlayerDifficultyState playerState = telemetry != null ? telemetry.GetPlayerState() : default;

        // BUG-03 fix: the live (non-episode) update path has no episode outcome, so we
        // conservatively skip the HP penalty when the player is not taking ongoing damage —
        // rely on damageTakenPerSecond and currentPressure as the reduction signals instead.
        float desiredDifficulty = currentDifficulty;
        if (currentPressure > maxTargetPressure || playerState.damageTakenPerSecond > 0.4f)
        {
            desiredDifficulty -= maxStepChange;
        }
        else if (currentPressure < minTargetPressure && playerState.healthPercent > 0.6f)
        {
            desiredDifficulty += maxStepChange;
        }

        if (playerState.hitRate > 0.45f && playerState.damageDealtPerSecond > playerState.damageTakenPerSecond)
            desiredDifficulty += maxStepChange * 0.5f;

        if (playerState.nearMissesPerSecond > 1.5f)
            desiredDifficulty -= maxStepChange * 0.5f;

        currentDifficulty = Mathf.Clamp01(Mathf.MoveTowards(currentDifficulty, desiredDifficulty, maxStepChange));
        currentProfile = DifficultyProfile.FromPressure(currentDifficulty, maxActiveEnemyBullets);
        ApplyProfileToScene();

        if (debugLogging)
        {
            Debug.Log($"DDA difficulty={currentDifficulty:F2}, pressure={currentPressure:F2}, hp={playerState.healthPercent:F2}, hitRate={playerState.hitRate:F2}");
        }
    }

    private void ApplyProfileToScene()
    {
        // BUG-04 fix: iterate the pre-registered list instead of scanning all
        // MonoBehaviours in the scene on every call.
        for (int i = _tunables.Count - 1; i >= 0; i--)
        {
            if (_tunables[i] == null)
            {
                _tunables.RemoveAt(i); // clean up destroyed objects
                continue;
            }
            _tunables[i].ApplyDifficulty(currentProfile);
        }
    }

    public void ForceSetDifficulty(float normalizedValue)
    {
        currentDifficulty = Mathf.Clamp01(normalizedValue);
        currentProfile = DifficultyProfile.FromPressure(currentDifficulty, maxActiveEnemyBullets);
        ApplyProfileToScene();
    }

    /// <summary>
    /// Called by DDAAgent to apply a custom difficulty profile directly,
    /// bypassing rule-based logic. Updates currentProfile and pushes to all tunables.
    /// </summary>
    public void ApplyAgentProfile(DifficultyProfile profile)
    {
        profile.Clamp();
        currentProfile = profile;
        ApplyProfileToScene();
    }


    /// <summary>
    /// Called by RLTrainingManager at episode end.
    /// avgEpisodePressure should be the episode-averaged pressure sampled every frame
    /// (not a point-in-time snapshot at episode end, which is unreliable).
    /// </summary>
    public void OnEpisodeEnd(int currentEpisodeCount, float avgEpisodePressure = -1f)
    {
        // No difficulty changes allowed for the first 2 episodes
        if (currentEpisodeCount < 2)
            return;

        // 2 episode cooldown between any difficulty change (next allowed change at lastChangedEpisode + 3)
        if (lastChangedEpisode >= 0 && currentEpisodeCount - lastChangedEpisode < 3)
            return;

        // Prefer the episode-averaged pressure passed in from RLTrainingManager.
        // Fall back to a live snapshot only if no value was supplied.
        currentPressure = avgEpisodePressure >= 0f
            ? avgEpisodePressure
            : (pressureAnalyzer != null ? pressureAnalyzer.GetPressureScore() : 0f);

        PlayerDifficultyState playerState = telemetry != null ? telemetry.GetPlayerState() : default;

        // Only reduce difficulty for low HP when the player LOST.
        bool playerWon = trainingManager != null
            && trainingManager.lastEpisodeOutcome == "enemy_defeated";

        float desiredDifficulty = currentDifficulty;
        if (currentPressure > maxTargetPressure
            || (!playerWon && playerState.healthPercent < 0.35f)
            || playerState.damageTakenPerSecond > 0.4f)
        {
            desiredDifficulty -= 0.05f;
        }
        else if (currentPressure < minTargetPressure && playerState.healthPercent > 0.6f)
        {
            desiredDifficulty += 0.05f;
        }

        if (playerState.hitRate > 0.45f && playerState.damageDealtPerSecond > playerState.damageTakenPerSecond)
            desiredDifficulty += 0.05f * 0.5f;

        if (playerState.nearMissesPerSecond > 1.5f)
            desiredDifficulty -= 0.05f * 0.5f;

        // Clamp to the current tier's difficulty band so rule-based DDA
        // never escapes the range that the FuzzyTierClassifier assigned.
        float bandLow  = (int)_currentTier * 0.25f;
        float bandHigh = bandLow + 0.25f;

        // Clamp the total change to 0.05f max per episode, then clamp to band
        float delta = Mathf.Clamp(desiredDifficulty - currentDifficulty, -0.05f, 0.05f);
        float newDifficulty = Mathf.Clamp(currentDifficulty + delta, bandLow, bandHigh);

        if (Mathf.Abs(newDifficulty - currentDifficulty) > 0.0001f)
        {
            currentDifficulty = newDifficulty;
            lastChangedEpisode = currentEpisodeCount;
            currentProfile = DifficultyProfile.FromPressure(currentDifficulty, maxActiveEnemyBullets);
            ApplyProfileToScene();

            if (debugLogging)
                Debug.Log($"[DanmakuDDA] Episode {currentEpisodeCount}: difficulty {currentDifficulty:F2} " +
                          $"(tier={_currentTier}, band=[{bandLow:F2},{bandHigh:F2}], pressure={currentPressure:F2})");
        }
    }

    /// <summary>
    /// Called by RLTrainingManager after FuzzyTierClassifier evaluates each episode.
    /// Clamps currentDifficulty into the new tier's band immediately so gameplay
    /// reflects the escalation without waiting for the next DDA nudge.
    /// </summary>
    public void SetTier(DifficultyTier newTier)
    {
        if (newTier == _currentTier)
            return;

        DifficultyTier previous = _currentTier;
        _currentTier = newTier;

        // Set difficulty to the midpoint of the new tier's band
        // E.g., Easy=[0,0.25] -> 0.125f; Medium=[0.25,0.50] -> 0.375f; Hard=[0.50,0.75] -> 0.625f; Expert=[0.75,1.0] -> 0.875f
        float bandLow  = (int)newTier * 0.25f;
        float newDifficulty = bandLow + 0.125f;

        Debug.Log($"[DanmakuDDA] Tier {previous}→{newTier}: " +
                  $"difficulty set to midpoint {newDifficulty:F2} " +
                  $"(band=[{bandLow:F2},{(bandLow + 0.25f):F2}])");

        currentDifficulty = newDifficulty;
        currentProfile = DifficultyProfile.FromPressure(currentDifficulty, maxActiveEnemyBullets);
        ApplyProfileToScene();
    }
}
