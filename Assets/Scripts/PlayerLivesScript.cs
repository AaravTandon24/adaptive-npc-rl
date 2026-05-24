using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Events;

public class PlayerLivesScript : MonoBehaviour
{
    [Header("Health Settings")]
    public float currentHealth;
    public float maxHealth; 

    [Header("References")]
    public GameObject explosion;
    public Text livesText;
    public GameOverScript gameOverScript;

    private bool isDead = false;
    private PlayerPerformanceTelemetry telemetry;

    private void Start()
    {
        telemetry = GetComponent<PlayerPerformanceTelemetry>();
        if (currentHealth <= 0)
        {
            currentHealth = maxHealth; 
        }
        UpdateUI();
    }

    public UnityEvent OnDied;

    private void UpdateUI()
    {
        if (livesText != null)
        {
            livesText.text = currentHealth.ToString();
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (telemetry == null)
            telemetry = GetComponent<PlayerPerformanceTelemetry>();

        if (telemetry != null)
            telemetry.ReportDamageTaken(damage);

        UpdateUI();

        if (currentHealth <= 0 && !isDead)
        {
            OnDied.Invoke();
            GameOver();
        }
    }

    public void AddHealth(float health)
    {
        if (isDead || health <= 0) return;
        currentHealth += health;
        if (telemetry == null)
            telemetry = GetComponent<PlayerPerformanceTelemetry>();

        if (telemetry != null)
            telemetry.ReportPowerupCollected();

        UpdateUI();
    }

    private void GameOver()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Game Over!");

        if (explosion != null)
        {
            GameObject exp = Instantiate(explosion, transform.position, Quaternion.identity);
            StartCoroutine(GameOverSequence(exp));
        }
        gameObject.SetActive(false);
        gameOverScript.Setup();

    }

    private IEnumerator GameOverSequence(GameObject exp)
    {
        yield return new WaitForSeconds(3f);

        if (exp != null)
        {
            Destroy(exp);
        }

        Time.timeScale = 0;
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("HealthPowerUp"))
        { 
            AddHealth(2);
            if (currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
                UpdateUI();
            }
            Destroy(collision.gameObject);
        }
    }

    public float RemainingHealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f; // Avoid division by zero.
}
