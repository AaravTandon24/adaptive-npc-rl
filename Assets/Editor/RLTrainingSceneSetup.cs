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
    private const int VectorObservationSize = 9;
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
        behaviorParameters.BehaviorType = BehaviorType.Default;
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

        string targetPath = Path.Combine(assetDirectory, Path.GetFileName(latestDataFile));
        if (File.Exists(targetPath))
            FileUtil.DeleteFileOrDirectory(targetPath);

        FileUtil.CopyFileOrDirectory(latestDataFile, targetPath);
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
    }
}
