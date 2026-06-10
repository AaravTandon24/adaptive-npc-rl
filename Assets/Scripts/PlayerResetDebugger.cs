using UnityEngine;

/// <summary>
/// Attach this to the PLAYER GameObject in the Testing scene.
/// It logs every lifecycle event and key state values so we can
/// pinpoint exactly why movement/shooting breaks after death.
/// Remove after debugging.
/// </summary>
public class PlayerResetDebugger : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerLivesScript lives;
    private PlayerMovement movement;
    private Shooting shooting;
    private int framesSinceEnable = 0;
    private bool wasDeadLastFrame = false;

    void Awake()
    {
        CacheRefs();
        Debug.Log($"[PlayerResetDebugger] Awake() called. Frame={Time.frameCount}");
        LogFullState("Awake");
    }

    void OnEnable()
    {
        CacheRefs();
        framesSinceEnable = 0;
        Debug.Log($"[PlayerResetDebugger] OnEnable() called. Frame={Time.frameCount}");
        LogFullState("OnEnable");
    }

    void OnDisable()
    {
        Debug.Log($"[PlayerResetDebugger] OnDisable() called. Frame={Time.frameCount}");
        LogFullState("OnDisable");
        // Log the call stack so we know WHO disabled us
        Debug.Log($"[PlayerResetDebugger] Disable callstack:\n{System.Environment.StackTrace}");
    }

    void Update()
    {
        framesSinceEnable++;

        // Log for the first 5 frames after re-enable to catch issues
        if (framesSinceEnable <= 5)
        {
            float hInput = Input.GetAxisRaw("Horizontal");
            float vInput = Input.GetAxisRaw("Vertical");
            bool fire = Input.GetButtonDown("Fire1");
            Debug.Log($"[PlayerResetDebugger] Update frame {framesSinceEnable} after enable: " +
                      $"H={hInput} V={vInput} Fire={fire} " +
                      $"timeScale={Time.timeScale} deltaTime={Time.deltaTime:F4} " +
                      $"activeInHierarchy={gameObject.activeInHierarchy}");
        }

        // Detect death transition
        if (lives != null)
        {
            bool isDeadNow = lives.currentHealth <= 0;
            if (isDeadNow && !wasDeadLastFrame)
            {
                Debug.LogWarning($"[PlayerResetDebugger] DEATH DETECTED! " +
                                 $"health={lives.currentHealth} Frame={Time.frameCount}");
                LogFullState("DeathDetected");
            }
            wasDeadLastFrame = isDeadNow;
        }
    }

    void FixedUpdate()
    {
        if (framesSinceEnable <= 3)
        {
            Debug.Log($"[PlayerResetDebugger] FixedUpdate frame {framesSinceEnable}: " +
                      $"rb.isKinematic={rb?.isKinematic} " +
                      $"rb.bodyType={rb?.bodyType} " +
                      $"rb.velocity={rb?.velocity} " +
                      $"rb.constraints={rb?.constraints} " +
                      $"rb.simulated={rb?.simulated} " +
                      $"position={transform.position}");
        }
    }

    private void CacheRefs()
    {
        rb = GetComponent<Rigidbody2D>();
        lives = GetComponent<PlayerLivesScript>();
        movement = GetComponent<PlayerMovement>();
        shooting = GetComponent<Shooting>();
    }

    private void LogFullState(string context)
    {
        Debug.Log($"[PlayerResetDebugger] === {context} STATE DUMP ===\n" +
                  $"  GameObject active: {gameObject.activeInHierarchy}\n" +
                  $"  Position: {transform.position}\n" +
                  $"  Time.timeScale: {Time.timeScale}\n" +
                  $"  --- PlayerLivesScript ---\n" +
                  $"  lives component: {(lives != null ? "found" : "NULL")}\n" +
                  $"  lives.enabled: {lives?.enabled}\n" +
                  $"  lives.currentHealth: {lives?.currentHealth}\n" +
                  $"  lives.maxHealth: {lives?.maxHealth}\n" +
                  $"  lives.trainingMode: {lives?.trainingMode}\n" +
                  $"  --- Rigidbody2D ---\n" +
                  $"  rb component: {(rb != null ? "found" : "NULL")}\n" +
                  $"  rb.bodyType: {rb?.bodyType}\n" +
                  $"  rb.isKinematic: {rb?.isKinematic}\n" +
                  $"  rb.simulated: {rb?.simulated}\n" +
                  $"  rb.constraints: {rb?.constraints}\n" +
                  $"  rb.velocity: {rb?.velocity}\n" +
                  $"  --- PlayerMovement ---\n" +
                  $"  movement component: {(movement != null ? "found" : "NULL")}\n" +
                  $"  movement.enabled: {movement?.enabled}\n" +
                  $"  movement.rb: {(movement?.rb != null ? "found" : "NULL")}\n" +
                  $"  movement.cam: {(movement?.cam != null ? "found" : "NULL")}\n" +
                  $"  movement.moveSpeed: {movement?.moveSpeed}\n" +
                  $"  --- Shooting ---\n" +
                  $"  shooting component: {(shooting != null ? "found" : "NULL")}\n" +
                  $"  shooting.enabled: {shooting?.enabled}\n" +
                  $"  shooting.firePoint: {(shooting?.firePoint != null ? "found" : "NULL")}\n" +
                  $"  shooting.bulletPrefab: {(shooting?.bulletPrefab != null ? "found" : "NULL")}\n" +
                  $"  ==============================");
    }
}
