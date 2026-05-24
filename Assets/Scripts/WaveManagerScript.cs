using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.UI;

public class WaveManager : MonoBehaviour, IDifficultyTunable
{
    public Wave[] waves;
    public Transform[] spawnPoints;  // Array of spawn points
    public UnityEvent onWaveStart;
    public UnityEvent onWaveEnd;
    public UnityEvent onAllWavesCompleted;
    public Text waveText;

    private int currentWaveIndex = 0;
    private int enemiesRemaining = 0;
    private int enemiesSpawned = 0;
    private bool isSpawning = false;
    private DifficultyProfile difficultyProfile = DifficultyProfile.Default;

    public int CurrentWave => currentWaveIndex + 1;
    public int TotalWaves => waves.Length;
    public int RemainingEnemies => enemiesRemaining;

    void Start()
    {
        DanmakuDDAController.EnsureExists().RegisterTunable(this);

        StartCoroutine(StartWaveSystem());
    }

    IEnumerator StartWaveSystem()
    {
        yield return new WaitForSeconds(3f);  // Initial game delay
        StartNextWave();
    }

    void StartNextWave()
    {
        if (currentWaveIndex < waves.Length)
        {
            Wave currentWave = waves[currentWaveIndex];
            int curwave = currentWaveIndex + 1;
            waveText.text = "Wave: " + curwave.ToString();
            enemiesRemaining = currentWave.numberOfEnemies;
            enemiesSpawned = 0;

            onWaveStart?.Invoke();
            StartCoroutine(SpawnWaveEnemies(currentWave));
        }
        else
        {
            onAllWavesCompleted?.Invoke();
            Debug.Log("All waves completed!");
        }
    }

    IEnumerator SpawnWaveEnemies(Wave wave)
    {
        isSpawning = true;

        while (enemiesSpawned < wave.numberOfEnemies)
        {
            SpawnEnemy(wave);
            enemiesSpawned++;
            yield return new WaitForSeconds(GetAdjustedSpawnInterval(wave));
        }

        isSpawning = false;
    }

    void SpawnEnemy(Wave wave)
    {
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemyPrefab = wave.enemyPrefabs[Random.Range(0, wave.enemyPrefabs.Length)];

        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

        EnemyScript enemyScript = enemy.GetComponent<EnemyScript>();
        if (enemyScript != null)
        {
            enemyScript.speed += wave.difficultyModifier;
        }

        EnemyHealthScript healthScript = enemy.GetComponent<EnemyHealthScript>();
        if (healthScript != null)
        {
            healthScript.OnEnemyDeath += OnEnemyDefeated;
        }
    }

    public void ApplyDifficulty(DifficultyProfile profile)
    {
        difficultyProfile = profile;
    }

    private float GetAdjustedSpawnInterval(Wave wave)
    {
        return Mathf.Max(0.1f, wave.spawnInterval * difficultyProfile.spawnIntervalMultiplier);
    }

    void OnEnemyDefeated()
    {
        enemiesRemaining--;
        Debug.Log("Enemy defeated! Remaining: " + enemiesRemaining);

        if (enemiesRemaining <= 0 && !isSpawning)
        {
            Debug.Log("Wave " + (currentWaveIndex + 1) + " completed!");
            onWaveEnd?.Invoke();
            currentWaveIndex++;
            StartCoroutine(StartNextWaveAfterDelay());
        }
    }

    IEnumerator StartNextWaveAfterDelay()
    {
        if (currentWaveIndex < waves.Length)
        {
            Debug.Log("Starting wave " + (currentWaveIndex + 1) + " after delay...");
            yield return new WaitForSeconds(waves[currentWaveIndex - 1].timeBetweenWaves);
            StartNextWave();
        }
        else
        {
            Debug.Log("No more waves left!");
        }
    }
}
