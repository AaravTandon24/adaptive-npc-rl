using UnityEngine;

[System.Serializable]
public class Wave
{
    public string waveName;
    public int numberOfEnemies;
    public GameObject[] enemyPrefabs;  // Different types of enemies
    public float spawnInterval;
    public float timeBetweenWaves;
    public int difficultyModifier;  // Increases enemy stats for this wave
}