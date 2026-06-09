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

    /// <summary>
    /// Set to true by RLTrainingManager so that player death triggers an
    /// episode reset instead of the normal Game Over sequence.
    /// When true, GameOver() is never called and Time.timeScale is never set to 0.
    /// </summary>
    [HideInInspector] public bool trainingMode = false;

    private bool isDead = false;
    private PlayerPerformanceTelemetry telemetry;
    private Rigidbody2D rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        telemetry = GetComponent<PlayerPerformanceTelemetry>();
        if (currentHealth <= 0)
            currentHealth = maxHealth;
        UpdateUI();
    }

    public UnityEvent OnDied;
    [HideInInspector] public UnityEvent OnDiedTraining = new UnityEvent();

    private void UpdateUI()
    {
        if (livesText != null)
            livesText.text = currentHealth.ToString();
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
            isDead = true;
            Debug.Log($"[PlayerLives] Player died. trainingMode={trainingMode}, timeScale={Time.timeScale:F2}");

            if (trainingMode)
            {
                Debug.Log("[PlayerLives] trainingMode=true: invoking OnDiedTraining and skipping normal OnDied/GameOver.");
                OnDiedTraining.Invoke();
                return;
            }

            Debug.Log("[PlayerLives] trainingMode=false: invoking normal OnDied and calling GameOver().");
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
        Debug.Log("Game Over!");

        if (explosion != null)
        {
            GameObject exp = Instantiate(explosion, transform.position, Quaternion.identity);
            StartCoroutine(GameOverSequence(exp));
        }

        gameObject.SetActive(false);

        if (gameOverScript != null)
            gameOverScript.Setup();
    }

    private IEnumerator GameOverSequence(GameObject exp)
    {
        yield return new WaitForSeconds(3f);

        if (exp != null)
            Destroy(exp);

        Time.timeScale = 0;
    }

    // -------------------------------------------------------------------------
    // Training-mode reset API — called by RLTrainingManager
    // -------------------------------------------------------------------------

    public void ResetForEpisode()
    {
        isDead = false;
        currentHealth = maxHealth;
        Time.timeScale = 1f;

        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.simulated = true;
            rb.WakeUp();
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Restore focus to the Game View so keyboard input (WASD) works
        // after the reset. Without this, the Console/Inspector can steal focus
        // and Input.GetAxisRaw returns 0 the entire episode.
#if UNITY_EDITOR
        System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType != null)
        {
            UnityEditor.EditorWindow gv = UnityEditor.EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gv != null) gv.Focus();
        }
#endif

        UpdateUI();
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

    public float RemainingHealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
}
