using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadSpawnScript : MonoBehaviour
{
    public GameObject enemy;
    public float leastWait;
    public float mostWait;
    public float startWait;
    public float spawnWait;
    public bool stop;

    void Start()
    {
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
}
