using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadSpawnScript : MonoBehaviour, IDifficultyTunable
{
    public GameObject enemy;
    public float leastWait;
    public float mostWait;
    public float startWait;
    public float spawnWait;
    public bool stop;
    private float baseLeastWait;
    private float baseMostWait;
    private float baseStartWait;

    private void Awake()
    {
        baseLeastWait = leastWait;
        baseMostWait = mostWait;
        baseStartWait = startWait;
    }

    void Start()
    {
        baseLeastWait = leastWait;
        baseMostWait = mostWait;
        baseStartWait = startWait;

        DanmakuDDAController.EnsureExists().RegisterTunable(this);

        StartCoroutine(SpawnEnemy());
    }

    void Update()
    {
        spawnWait = Random.Range(leastWait, mostWait);
    }

    IEnumerator SpawnEnemy()
    {
        yield return new WaitForSeconds(startWait);

        while (!stop)
        {
            Instantiate(enemy, transform.position, transform.rotation);
            yield return new WaitForSeconds(spawnWait);
        }
    }

    public void ApplyDifficulty(DifficultyProfile profile)
    {
        leastWait = Mathf.Max(0.1f, baseLeastWait * profile.spawnIntervalMultiplier);
        mostWait = Mathf.Max(leastWait, baseMostWait * profile.spawnIntervalMultiplier);
        startWait = Mathf.Max(0f, baseStartWait * profile.spawnIntervalMultiplier);
    }
}
