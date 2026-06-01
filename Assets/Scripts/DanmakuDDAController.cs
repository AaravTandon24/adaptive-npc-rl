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
    public float updateInterval = 3f;          // increased interval to slow cadence
    public float maxStepChange = 0.04f;        // reduced per-step change
    public int maxActiveEnemyBullets = 120;
    public bool debugLogging = false;

    [Header("Smoothing / Hysteresis")]
    [Range(0f, 0.2f)] public float pressureHysteresis = 0.05f; // dead zone around targets
    public float difficultySmoothTime = 2f; // seconds to smooth toward desired difficulty

    [Header("Runtime State")]
    [Range(0f, 1f)] public float currentDifficulty = 0.5f;
    [Range(0f, 1f)] public float currentPressure;
    public DifficultyProfile currentProfile = DifficultyProfile.Default;

    public DifficultyProfile CurrentProfile => currentProfile;

    private float nextUpdateTime;
    private float difficultyVelocity = 0f;

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
        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + Mathf.Max(0.25f, updateInterval);
        MonitorAnalyzePlanExecute();
    }

    public void RegisterTunable(IDifficultyTunable tunable)
    {
        if (tunable != null)
            tunable.ApplyDifficulty(currentProfile);
    }

    private void MonitorAnalyzePlanExecute()
    {
        currentPressure = pressureAnalyzer != null ? pressureAnalyzer.GetPressureScore() : 0f;
        PlayerDifficultyState playerState = telemetry != null ? telemetry.GetPlayerState() : default;

        float desiredDifficulty = currentDifficulty;

        // Decrease difficulty if pressure is high or player struggling
        if (currentPressure > (maxTargetPressure + pressureHysteresis) ||
            playerState.healthPercent < 0.35f ||
            playerState.damageTakenPerSecond > 0.4f)
        {
            desiredDifficulty -= maxStepChange;
        }
        // Increase difficulty if pressure is notably low AND player is healthy AND engaged
        else if (currentPressure < (minTargetPressure - pressureHysteresis) && playerState.healthPercent > 0.6f)
        {
            // Require some player engagement to avoid rewarding pure dodging:
            // either minimal firing activity or a non-trivial hit rate
            if (playerState.shotsPerSecond > 0.15f || playerState.hitRate > 0.25f)
            {
                desiredDifficulty += maxStepChange;
            }
        }

        // Extra adjustments (same as before)
        if (playerState.hitRate > 0.45f && playerState.damageDealtPerSecond > playerState.damageTakenPerSecond)
            desiredDifficulty += maxStepChange * 0.5f;

        if (playerState.nearMissesPerSecond > 1.5f)
            desiredDifficulty -= maxStepChange * 0.5f;

        // Clamp desiredDifficulty
        desiredDifficulty = Mathf.Clamp01(desiredDifficulty);

        // Smooth the transition toward desired difficulty over time
        // SmoothDamp is used so changes integrate over multiple updates rather than stepping abruptly.
        float smoothDeltaTime = Mathf.Max(0.0001f, updateInterval); // approximate cadence to SmoothDamp
        currentDifficulty = Mathf.Clamp01(Mathf.SmoothDamp(currentDifficulty, desiredDifficulty, ref difficultyVelocity, difficultySmoothTime, Mathf.Infinity, smoothDeltaTime));

        currentProfile = DifficultyProfile.FromPressure(currentDifficulty, maxActiveEnemyBullets);
        ApplyProfileToScene();

        if (debugLogging)
        {
            Debug.Log($"DDA difficulty={currentDifficulty:F2}, pressure={currentPressure:F2}, hp={playerState.healthPercent:F2}, hitRate={playerState.hitRate:F2}");
        }
    }

    private void ApplyProfileToScene()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            IDifficultyTunable tunable = behaviour as IDifficultyTunable;
            if (tunable != null)
                tunable.ApplyDifficulty(currentProfile);
        }
    }
}
