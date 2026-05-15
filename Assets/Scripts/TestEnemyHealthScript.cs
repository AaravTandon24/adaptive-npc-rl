using UnityEngine;

/// <summary>
/// Health script for test enemies in RL training environment.
/// Does NOT trigger death events or destruction - managed by RLTrainingManager instead.
/// </summary>
public class TestEnemyHealthScript : MonoBehaviour
{
    public float currentHealth;
    public float maxHealth;

    private void Start()
    {
        if (currentHealth <= 0)
        {
            currentHealth = maxHealth;
        }
    }

    /// <summary>
    /// Apply damage to the enemy. Does not trigger death - RLTrainingManager handles episode end.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        // Do NOT call Die() - let RLTrainingManager detect health <= 0 and handle reset
        if (currentHealth <= 0)
        {
            Debug.Log("Test enemy health reached 0 (managed by RLTrainingManager)");
        }
    }

    /// <summary>
    /// Reset health to max (called by RLTrainingManager)
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }
}