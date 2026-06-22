using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.Sentis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RLTrainingSceneSetup
{
    private const string TrainingScenePath = "Assets/Scenes/Testing.unity";
    private const string TrainingBuildPath = "Builds/Training/adaptive-npc-rl-training.exe";
    private const string TrainedModelSourcePath = "results/enemy_agent_ppo_initial/EnemyAgent.onnx";
    private const string TrainedModelCheckpointDirectory = "results/enemy_agent_ppo_initial/EnemyAgent";
    private const string TrainedModelAssetPath = "Assets/ML-Agents/Models/EnemyAgent.onnx";
    private const string BehaviorName = "EnemyAgent";
    private const int VectorObservationSize = 38;
    private const int ContinuousActionSize = 2;

    [MenuItem("Tools/RL/Configure Testing Scene")]
    public static void ConfigureTestingScene()
    {
        EditorSceneManager.OpenScene(TrainingScenePath);

        RLTrainingManager trainingManager = Object.FindObjectOfType<RLTrainingManager>();
        if (trainingManager == null)
        {
            Debug.LogError("RL setup failed: RLTrainingManager was not found in Testing.unity.");
            return;
        }

        if (trainingManager.enemy == null)
        {
            Debug.LogError("RL setup failed: RLTrainingManager.enemy is not assigned.");
            return;
        }

        // Set maxEpisodes to 0 (infinite) for continuous ML-Agents training
        trainingManager.maxEpisodes = 0;

        GameObject enemy = trainingManager.enemy;
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = enemy.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        EnemyAgent agent = enemy.GetComponent<EnemyAgent>();
        if (agent == null)
            agent = enemy.AddComponent<EnemyAgent>();

        if (trainingManager.player != null)
            agent.player = trainingManager.player.transform;

        BehaviorParameters behaviorParameters = enemy.GetComponent<BehaviorParameters>();
        if (behaviorParameters == null)
            behaviorParameters = enemy.AddComponent<BehaviorParameters>();

        behaviorParameters.BehaviorName = BehaviorName;
        
        // Freeze Agent 1 (EnemyAgent) during Agent 2 (DDAAgent) training
        ModelAsset v7Model = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/ML-Agents/Models/EnemyAgent_v7.onnx");
        if (v7Model != null)
        {
            behaviorParameters.Model = v7Model;
            behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
        }
        else
        {
            behaviorParameters.BehaviorType = BehaviorType.Default;
            Debug.LogWarning("EnemyAgent_v7.onnx model not found at Assets/ML-Agents/Models/EnemyAgent_v7.onnx. Set to Default behavior type.");
        }
        behaviorParameters.BrainParameters.VectorObservationSize = VectorObservationSize;
        behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
        behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(ContinuousActionSize);
        behaviorParameters.BrainParameters.VectorActionDescriptions = new[] { "Move X", "Move Y" };

        DecisionRequester decisionRequester = enemy.GetComponent<DecisionRequester>();
        if (decisionRequester == null)
            decisionRequester = enemy.AddComponent<DecisionRequester>();

        decisionRequester.DecisionPeriod = 5;
        decisionRequester.DecisionStep = 0;
        decisionRequester.TakeActionsBetweenDecisions = true;

        // Configure DDAAgent (Agent 2)
        GameObject ddaAgentGo = GameObject.Find("DDAAgent");
        if (ddaAgentGo == null)
        {
            ddaAgentGo = new GameObject("DDAAgent");
        }

        DDAAgent ddaAgent = ddaAgentGo.GetComponent<DDAAgent>();
        if (ddaAgent == null)
            ddaAgent = ddaAgentGo.AddComponent<DDAAgent>();

        // Wire DDAAgent dependencies
        ddaAgent.trainingManager = trainingManager;
        ddaAgent.tierClassifier = Object.FindObjectOfType<FuzzyTierClassifier>();

        // Wire trainingManager.ddaAgent
        trainingManager.ddaAgent = ddaAgent;

        BehaviorParameters ddaBehavior = ddaAgentGo.GetComponent<BehaviorParameters>();
        if (ddaBehavior == null)
            ddaBehavior = ddaAgentGo.AddComponent<BehaviorParameters>();

        ddaBehavior.BehaviorName = "DDAAgent";
        ddaBehavior.BehaviorType = BehaviorType.Default;
        ddaBehavior.BrainParameters.VectorObservationSize = 10;
        ddaBehavior.BrainParameters.NumStackedVectorObservations = 1;
        ddaBehavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);

        // Remove DecisionRequester if present on DDAAgent
        DecisionRequester ddaDecisionRequester = ddaAgentGo.GetComponent<DecisionRequester>();
        if (ddaDecisionRequester != null)
        {
            Object.DestroyImmediate(ddaDecisionRequester);
        }

        EditorUtility.SetDirty(ddaAgentGo);
        EditorUtility.SetDirty(enemy);
        EditorUtility.SetDirty(trainingManager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("RL setup complete: Testing.unity enemy is configured for PPO training.");
    }

    [MenuItem("Tools/RL/Build Training Player")]
    public static void BuildTrainingPlayer()
    {
        ConfigureTestingScene();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { TrainingScenePath },
            locationPathName = TrainingBuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(options);
        Debug.Log($"RL training player built at {TrainingBuildPath}");
    }

    [MenuItem("Tools/RL/Import Initial PPO Model")]
    public static void ImportInitialPPOModel()
    {
        if (!File.Exists(TrainedModelSourcePath))
        {
            Debug.LogError($"PPO model was not found at {TrainedModelSourcePath}.");
            return;
        }

        string assetDirectory = Path.GetDirectoryName(TrainedModelAssetPath);
        if (!Directory.Exists(assetDirectory))
            Directory.CreateDirectory(assetDirectory);

        if (File.Exists(TrainedModelAssetPath))
            FileUtil.DeleteFileOrDirectory(TrainedModelAssetPath);

        FileUtil.CopyFileOrDirectory(TrainedModelSourcePath, TrainedModelAssetPath);
        CopyLatestExternalWeights(assetDirectory);

        AssetDatabase.ImportAsset(TrainedModelAssetPath, ImportAssetOptions.ForceUpdate);

        ModelAsset model = AssetDatabase.LoadAssetAtPath<ModelAsset>(TrainedModelAssetPath);
        if (model == null)
        {
            Debug.LogError($"Unity could not import the PPO model at {TrainedModelAssetPath}.");
            return;
        }

        ConfigureTestingScene();

        RLTrainingManager trainingManager = Object.FindObjectOfType<RLTrainingManager>();
        BehaviorParameters behaviorParameters = trainingManager.enemy.GetComponent<BehaviorParameters>();
        behaviorParameters.Model = model;
        behaviorParameters.BehaviorType = BehaviorType.Default;

        EditorUtility.SetDirty(behaviorParameters);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"Imported and assigned PPO model: {TrainedModelAssetPath}");
    }

    private static void CopyLatestExternalWeights(string assetDirectory)
    {
        if (!Directory.Exists(TrainedModelCheckpointDirectory))
            return;

        string[] dataFiles = Directory.GetFiles(TrainedModelCheckpointDirectory, "*.onnx.data");
        if (dataFiles.Length == 0)
            return;

        string latestDataFile = dataFiles[0];
        for (int i = 1; i < dataFiles.Length; i++)
        {
            if (File.GetLastWriteTimeUtc(dataFiles[i]) > File.GetLastWriteTimeUtc(latestDataFile))
                latestDataFile = dataFiles[i];
        }

        string latestFileName = Path.GetFileName(latestDataFile);
        CopyExternalWeightsAlias(latestDataFile, assetDirectory, latestFileName);
        CopyExternalWeightsAlias(latestDataFile, assetDirectory, latestFileName.Replace("EnemyAgent", "enemy-agent"));
        CopyExternalWeightsAlias(latestDataFile, assetDirectory, latestFileName.Replace("50029", "50028"));
        CopyExternalWeightsAlias(latestDataFile, assetDirectory, latestFileName.Replace("EnemyAgent", "enemy-agent").Replace("50029", "50028"));
    }

    private static void CopyExternalWeightsAlias(string sourcePath, string assetDirectory, string targetFileName)
    {
        string targetPath = Path.Combine(assetDirectory, targetFileName);
        if (File.Exists(targetPath))
            FileUtil.DeleteFileOrDirectory(targetPath);

        FileUtil.CopyFileOrDirectory(sourcePath, targetPath);
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
    }
}
