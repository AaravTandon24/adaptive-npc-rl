using UnityEngine;
using UnityEngine.Events;

public class EnemyHealthScript : MonoBehaviour
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

    public UnityEvent OnDied;

    public void TakeDamage(float damage)
    {
        if (damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public event System.Action OnEnemyDeath;

    public void Die()
    {
        Debug.Log("Die function called");
        OnDied?.Invoke();
        OnEnemyDeath?.Invoke();
    }
}
