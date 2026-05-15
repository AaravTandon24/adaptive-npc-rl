using UnityEngine;
using System.Collections.Generic;

public class PowerupSpawner : MonoBehaviour
{
    // Serialized fields to allow configuration in Unity Inspector
    [System.Serializable]
    public class PowerupData
    {
        public GameObject powerupPrefab;
        [Range(0f, 1f)]
        public float spawnChance = 0.5f;
    }

    [Header("Spawn Settings")]
    [Tooltip("List of possible powerup prefabs to spawn")]
    public List<PowerupData> availablePowerups = new List<PowerupData>();

    [Header("Spawner Locations")]
    [Tooltip("List of transform points where powerups can spawn")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Spawn Parameters")]
    [Tooltip("Minimum time between powerup spawns")]
    public float minSpawnInterval = 5f;
    [Tooltip("Maximum time between powerup spawns")]
    public float maxSpawnInterval = 15f;

    // Private variables for spawn tracking
    private float nextSpawnTime;

    private void Start()
    {
        // Initialize the first spawn time
        ResetSpawnTime();
    }

    private void Update()
    {
        // Check if it's time to spawn a powerup
        if (Time.time >= nextSpawnTime)
        {
            SpawnPowerup();
            ResetSpawnTime();
        }
    }

    /// <summary>
    /// Spawns a random powerup at a random spawn point
    /// </summary>
    private void SpawnPowerup()
    {
        // Check if we have spawn points and available powerups
        if (spawnPoints.Count == 0 || availablePowerups.Count == 0)
        {
            Debug.LogWarning("No spawn points or powerups configured!");
            return;
        }

        // Select a random spawn point
        Transform selectedSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

        // Select a powerup based on spawn chances
        PowerupData selectedPowerup = GetRandomPowerup();

        if (selectedPowerup != null && selectedPowerup.powerupPrefab != null)
        {
            // Instantiate the powerup at the selected spawn point
            Instantiate(selectedPowerup.powerupPrefab, selectedSpawnPoint.position, Quaternion.identity);
        }
    }

    /// <summary>
    /// Selects a random powerup based on their individual spawn chances
    /// </summary>
    /// <returns>Selected PowerupData or null</returns>
    private PowerupData GetRandomPowerup()
    {
        float totalChance = 0f;
        foreach (var powerup in availablePowerups)
        {
            totalChance += powerup.spawnChance;
        }

        float randomValue = Random.Range(0f, totalChance);
        float cumulativeChance = 0f;

        foreach (var powerup in availablePowerups)
        {
            cumulativeChance += powerup.spawnChance;
            if (randomValue <= cumulativeChance)
            {
                return powerup;
            }
        }

        return null;
    }

    /// <summary>
    /// Resets the next spawn time to a random interval
    /// </summary>
    private void ResetSpawnTime()
    {
        nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    /// <summary>
    /// Adds a new spawn point to the list
    /// </summary>
    /// <param name="spawnPoint">Transform to add as a spawn point</param>
    public void AddSpawnPoint(Transform spawnPoint)
    {
        if (!spawnPoints.Contains(spawnPoint))
        {
            spawnPoints.Add(spawnPoint);
        }
    }

    /// <summary>
    /// Removes a spawn point from the list
    /// </summary>
    /// <param name="spawnPoint">Transform to remove from spawn points</param>
    public void RemoveSpawnPoint(Transform spawnPoint)
    {
        spawnPoints.Remove(spawnPoint);
    }

    /// <summary>
    /// Adds a new powerup to the available powerups list
    /// </summary>
    /// <param name="powerupPrefab">Powerup prefab to add</param>
    /// <param name="spawnChance">Chance of this powerup spawning (0-1)</param>
    public void AddPowerup(GameObject powerupPrefab, float spawnChance = 0.5f)
    {
        PowerupData newPowerupData = new PowerupData
        {
            powerupPrefab = powerupPrefab,
            spawnChance = Mathf.Clamp01(spawnChance)
        };

        availablePowerups.Add(newPowerupData);
    }
}