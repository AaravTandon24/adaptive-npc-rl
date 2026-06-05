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
        if (tunable != null)
            tunable.ApplyDifficulty(currentProfile);
    }

    private void MonitorAnalyzePlanExecute()
    {
        currentPressure = pressureAnalyzer != null ? pressureAnalyzer.GetPressureScore() : 0f;
        PlayerDifficultyState playerState = telemetry != null ? telemetry.GetPlayerState() : default;

        float desiredDifficulty = currentDifficulty;
        if (currentPressure > maxTargetPressure || playerState.healthPercent < 0.35f || playerState.damageTakenPerSecond > 0.4f)
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
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            IDifficultyTunable tunable = behaviour as IDifficultyTunable;
            if (tunable != null)
                tunable.ApplyDifficulty(currentProfile);
        }
    }

    public void OnEpisodeEnd(int currentEpisodeCount)
    {
        // No difficulty changes allowed for the first 5 episodes
        if (currentEpisodeCount < 2)
            return;

        // 2 episode cooldown between any difficulty change (next allowed change at lastChangedEpisode + 3)
        if (lastChangedEpisode >= 0 && currentEpisodeCount - lastChangedEpisode < 3)
            return;

        currentPressure = pressureAnalyzer != null ? pressureAnalyzer.GetPressureScore() : 0f;
        PlayerDifficultyState playerState = telemetry != null ? telemetry.GetPlayerState() : default;

        float desiredDifficulty = currentDifficulty;
        if (currentPressure > maxTargetPressure || playerState.healthPercent < 0.35f || playerState.damageTakenPerSecond > 0.4f)
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

        // Clamp the total change to 0.05f max per episode
        float delta = Mathf.Clamp(desiredDifficulty - currentDifficulty, -0.05f, 0.05f);

        if (Mathf.Abs(delta) > 0.0001f)
        {
            currentDifficulty = Mathf.Clamp01(currentDifficulty + delta);
            lastChangedEpisode = currentEpisodeCount;
            currentProfile = DifficultyProfile.FromPressure(currentDifficulty, maxActiveEnemyBullets);
            ApplyProfileToScene();
        }
    }
}
